using System;
using System.Collections.Generic;

using Unity.Mathematics;

namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    ///     Maintains secondary indices for chunk state queries: sets of chunks by state,
    ///     mesh priority selection, and light update tracking.
    ///     Internal helper owned by <see cref="ChunkManager" />.
    /// </summary>
    internal sealed class ChunkStateIndex
    {
        /// <summary>
        ///     Maps face index to its opposite: +X(0) to -X(1), +Y(2) to -Y(3), +Z(4) to -Z(5).
        ///     Duplicated from ChunkManager for use in SetChunkState neighbor bitmask updates.
        /// </summary>
        private static readonly int[] s_oppositeFace =
        {
            1,
            0,
            3,
            2,
            5,
            4,
        };

        /// <summary>Secondary index: chunks in the Generated state (excluding LightJobInFlight).</summary>
        private readonly HashSet<ManagedChunk> _generatedChunks = new();

        /// <summary>Secondary index: chunks in the Ready state.</summary>
        private readonly HashSet<ManagedChunk> _readyChunks = new();

        /// <summary>Secondary index: chunks in the RelightPending state.</summary>
        private readonly HashSet<ManagedChunk> _relightPendingChunks = new();

        /// <summary>Secondary index: coordinates of chunks with NeedsLightUpdate = true.</summary>
        private readonly HashSet<int3> _chunksNeedingLightUpdate = new();

        /// <summary>Scratch buffer for FillChunksToMesh top-K selection.</summary>
        private readonly List<ManagedChunk> _meshCandidateCache = new();

        /// <summary>Parallel score array for FillChunksToMesh insertion sort.</summary>
        private readonly List<int> _meshScoreCache = new();

        /// <summary>Number of chunks in the Generated state (mesh-eligible).</summary>
        internal int GeneratedChunkCount
        {
            get { return _generatedChunks.Count; }
        }

        /// <summary>Creates a ChunkStateIndex with empty secondary indices.</summary>
        internal ChunkStateIndex()
        {
        }

        /// <summary>
        ///     Centralized state transition. Maintains all secondary indices
        ///     (_generatedChunks, _readyChunks, _relightPendingChunks).
        ///     All external callers (schedulers) MUST use this instead of chunk.State = x.
        /// </summary>
        internal void SetChunkState(ManagedChunk chunk, ChunkState newState)
        {
            ChunkState oldState = chunk.State;
            chunk.State = newState;

            // --- _generatedChunks ---
            if (oldState == ChunkState.Generated && newState != ChunkState.Generated)
            {
                _generatedChunks.Remove(chunk);
            }
            else if (newState == ChunkState.Generated && oldState != ChunkState.Generated)
            {
                if (!chunk.LightJobInFlight)
                {
                    _generatedChunks.Add(chunk);
                }
            }

            // --- _readyChunks ---
            if (oldState == ChunkState.Ready && newState != ChunkState.Ready)
            {
                _readyChunks.Remove(chunk);
            }
            else if (newState == ChunkState.Ready && oldState != ChunkState.Ready)
            {
                _readyChunks.Add(chunk);
            }

            // --- _relightPendingChunks ---
            if (oldState == ChunkState.RelightPending && newState != ChunkState.RelightPending)
            {
                _relightPendingChunks.Remove(chunk);
            }
            else if (newState == ChunkState.RelightPending && oldState != ChunkState.RelightPending)
            {
                _relightPendingChunks.Add(chunk);
            }

            // --- Neighbor ReadyNeighborMask maintenance ---
            // When this chunk crosses the RelightPending threshold (in either direction),
            // flip the corresponding bit on each existing neighbor's mask.
            bool wasReady = oldState >= ChunkState.RelightPending;
            bool isReady = newState >= ChunkState.RelightPending;

            if (wasReady != isReady)
            {
                for (int f = 0; f < 6; f++)
                {
                    ManagedChunk neighbor = chunk.Neighbors[f];

                    if (neighbor == null)
                    {
                        continue;
                    }

                    int oppF = s_oppositeFace[f];

                    if (isReady)
                    {
                        neighbor.ReadyNeighborMask |= (byte)(1 << oppF);
                    }
                    else
                    {
                        neighbor.ReadyNeighborMask &= (byte)~(1 << oppF);
                    }
                }
            }
        }

        /// <summary>
        ///     Called after any change to chunk.LightJobInFlight. Keeps _generatedChunks
        ///     in sync: a Generated chunk with LightJobInFlight=true is NOT mesh-eligible.
        /// </summary>
        internal void NotifyLightJobChanged(ManagedChunk chunk)
        {
            if (chunk.State == ChunkState.Generated)
            {
                if (chunk.LightJobInFlight)
                {
                    _generatedChunks.Remove(chunk);
                }
                else
                {
                    _generatedChunks.Add(chunk);
                }
            }
        }

        /// <summary>
        ///     Fills the provided list with Generated chunks, prioritized for meshing.
        ///     Priority: hasPlayerEdit (DESC) > neverMeshed (DESC) > hasAllNeighbors (DESC) > distScore (ASC).
        ///     distScore uses Manhattan distance (no rsqrt). Neighbor readiness uses eagerly
        ///     maintained ReadyNeighborMask bitmask (O(1) per chunk).
        ///     Uses a single-pass top-K selection over _generatedChunks.
        /// </summary>
        internal void FillChunksToMesh(
            List<ManagedChunk> result,
            int maxCount,
            int3 cameraChunkCoord,
            float3 cameraForwardXZ,
            IReadOnlyList<int3> lastPlayerChunkCoords)
        {
            result.Clear();
            _meshCandidateCache.Clear();
            _meshScoreCache.Clear();

            // --- Single-pass top-K selection over _generatedChunks ---
            foreach (ManagedChunk chunk in _generatedChunks)
            {
                bool hasEdit = chunk.HasPlayerEdit;
                bool neverMeshed = chunk.RenderedLODLevel < 0;
                bool allNeighbors = (chunk.ReadyNeighborMask & 0x3F) == 0x3F;

                // Distance = min Manhattan distance to any known player origin
                int distScore = int.MaxValue;

                if (lastPlayerChunkCoords.Count > 0)
                {
                    for (int p = 0; p < lastPlayerChunkCoords.Count; p++)
                    {
                        int3 d = chunk.Coord - lastPlayerChunkCoords[p];
                        int dist = math.abs(d.x) + math.abs(d.y) + math.abs(d.z);

                        if (dist < distScore)
                        {
                            distScore = dist;
                        }
                    }
                }
                else
                {
                    int3 d = chunk.Coord - cameraChunkCoord;
                    distScore = math.abs(d.x) + math.abs(d.y) + math.abs(d.z);
                }

                if (_meshCandidateCache.Count < maxCount)
                {
                    _meshCandidateCache.Add(chunk);
                    _meshScoreCache.Add(distScore);
                }
                else
                {
                    // Find worst element in buffer
                    int worstIdx = 0;

                    for (int w = 1; w < _meshCandidateCache.Count; w++)
                    {
                        if (IsWorseThan(w, worstIdx))
                        {
                            worstIdx = w;
                        }
                    }

                    // Compare incoming chunk against worst
                    bool worstHasEdit = _meshCandidateCache[worstIdx].HasPlayerEdit;
                    bool worstNeverMeshed = _meshCandidateCache[worstIdx].RenderedLODLevel < 0;
                    bool worstAllNeighbors = (_meshCandidateCache[worstIdx].ReadyNeighborMask & 0x3F) == 0x3F;
                    int worstScore = _meshScoreCache[worstIdx];

                    if (IsBetterPriority(hasEdit, neverMeshed, allNeighbors, distScore,
                            worstHasEdit, worstNeverMeshed, worstAllNeighbors, worstScore))
                    {
                        _meshCandidateCache[worstIdx] = chunk;
                        _meshScoreCache[worstIdx] = distScore;
                    }
                }
            }

            // Sort the small buffer (maxCount elements max — insertion sort is fine here)
            for (int i = 1; i < _meshCandidateCache.Count; i++)
            {
                ManagedChunk tempChunk = _meshCandidateCache[i];
                int tempScore = _meshScoreCache[i];
                bool tempHasEdit = tempChunk.HasPlayerEdit;
                bool tempNeverMeshed = tempChunk.RenderedLODLevel < 0;
                bool tempAllNeighbors = (tempChunk.ReadyNeighborMask & 0x3F) == 0x3F;
                int j = i - 1;

                while (j >= 0)
                {
                    bool jHasEdit = _meshCandidateCache[j].HasPlayerEdit;
                    bool jNeverMeshed = _meshCandidateCache[j].RenderedLODLevel < 0;
                    bool jAllNeighbors = (_meshCandidateCache[j].ReadyNeighborMask & 0x3F) == 0x3F;
                    int jScore = _meshScoreCache[j];

                    // Stop if current position is correct (j is better or equal)
                    if (!IsBetterPriority(tempHasEdit, tempNeverMeshed, tempAllNeighbors, tempScore,
                            jHasEdit, jNeverMeshed, jAllNeighbors, jScore))
                    {
                        break;
                    }

                    _meshCandidateCache[j + 1] = _meshCandidateCache[j];
                    _meshScoreCache[j + 1] = _meshScoreCache[j];
                    j--;
                }

                _meshCandidateCache[j + 1] = tempChunk;
                _meshScoreCache[j + 1] = tempScore;
            }

            for (int i = 0; i < _meshCandidateCache.Count; i++)
            {
                result.Add(_meshCandidateCache[i]);
            }
        }

        /// <summary>
        ///     Returns true if buffer element at indexA is worse priority than element at indexB.
        ///     Priority: hasPlayerEdit (DESC) > neverMeshed (DESC) > hasAllNeighbors (DESC) > distScore (ASC).
        /// </summary>
        private bool IsWorseThan(int indexA, int indexB)
        {
            bool aHasEdit = _meshCandidateCache[indexA].HasPlayerEdit;
            bool bHasEdit = _meshCandidateCache[indexB].HasPlayerEdit;

            if (aHasEdit != bHasEdit)
            {
                return !aHasEdit;
            }

            bool aNeverMeshed = _meshCandidateCache[indexA].RenderedLODLevel < 0;
            bool bNeverMeshed = _meshCandidateCache[indexB].RenderedLODLevel < 0;

            if (aNeverMeshed != bNeverMeshed)
            {
                return !aNeverMeshed;
            }

            bool aAllNeighbors = (_meshCandidateCache[indexA].ReadyNeighborMask & 0x3F) == 0x3F;
            bool bAllNeighbors = (_meshCandidateCache[indexB].ReadyNeighborMask & 0x3F) == 0x3F;

            if (aAllNeighbors != bAllNeighbors)
            {
                return !aAllNeighbors;
            }

            return _meshScoreCache[indexA] > _meshScoreCache[indexB];
        }

        /// <summary>
        ///     Returns true if (hasEdit, neverMeshed, allNeighbors, distScore) is strictly better
        ///     than (worstHasEdit, worstNeverMeshed, worstAllNeighbors, worstScore).
        ///     Better = hasEdit first, then neverMeshed, then allNeighbors, then lower distScore.
        /// </summary>
        private static bool IsBetterPriority(
            bool hasEdit, bool neverMeshed, bool allNeighbors, int distScore,
            bool worstHasEdit, bool worstNeverMeshed, bool worstAllNeighbors, int worstScore)
        {
            if (hasEdit != worstHasEdit)
            {
                return hasEdit;
            }

            if (neverMeshed != worstNeverMeshed)
            {
                return neverMeshed;
            }

            if (allNeighbors != worstAllNeighbors)
            {
                return allNeighbors;
            }

            return distScore < worstScore;
        }

        /// <summary>
        ///     Fills with all chunks in Generated state.
        ///     Used by LODScheduler to assign LOD levels before meshing.
        /// </summary>
        internal void FillGeneratedChunks(List<ManagedChunk> result)
        {
            result.Clear();

            foreach (ManagedChunk chunk in _generatedChunks)
            {
                result.Add(chunk);
            }
        }

        /// <summary>
        ///     Fills with Generated chunks that have LODLevel > 0.
        ///     These need LOD meshing, not full-detail meshing.
        /// </summary>
        internal void FillGeneratedChunksWithLOD(List<ManagedChunk> result)
        {
            result.Clear();

            foreach (ManagedChunk chunk in _generatedChunks)
            {
                if (chunk.LODLevel > 0)
                {
                    result.Add(chunk);
                }
            }
        }

        /// <summary>
        ///     Fills the provided list with all chunks in the Ready state.
        ///     Clears the list before filling. Used for LOD level assignment.
        ///     Uses _readyChunks secondary index — O(ready count) not O(all loaded).
        /// </summary>
        internal void FillReadyChunks(List<ManagedChunk> result)
        {
            result.Clear();

            foreach (ManagedChunk chunk in _readyChunks)
            {
                result.Add(chunk);
            }
        }

        /// <summary>
        ///     Fills the provided list with all chunks that need cross-chunk light updates.
        ///     Iterates only the dirty set instead of all loaded chunks — O(dirty) not O(all).
        ///     Clears the list before filling.
        /// </summary>
        internal void FillChunksNeedingLightUpdate(
            List<ManagedChunk> result,
            Func<int3, ManagedChunk> getChunk)
        {
            result.Clear();

            foreach (int3 coord in _chunksNeedingLightUpdate)
            {
                ManagedChunk chunk = getChunk(coord);

                if (chunk is not null && chunk.NeedsLightUpdate && !chunk.LightJobInFlight)
                {
                    result.Add(chunk);
                }
            }
        }

        /// <summary>
        ///     Marks a chunk as needing a cross-chunk light update and adds it to the dirty set.
        ///     Call this instead of setting chunk.NeedsLightUpdate = true directly.
        /// </summary>
        internal void MarkNeedsLightUpdate(int3 coord, ManagedChunk chunk)
        {
            if (chunk is not null)
            {
                chunk.NeedsLightUpdate = true;
                _chunksNeedingLightUpdate.Add(coord);
            }
        }

        /// <summary>
        ///     Clears the NeedsLightUpdate flag on a chunk and removes it from the dirty set.
        ///     Call this instead of setting chunk.NeedsLightUpdate = false directly.
        /// </summary>
        internal void ClearNeedsLightUpdate(int3 coord, ManagedChunk chunk)
        {
            if (chunk is not null)
            {
                chunk.NeedsLightUpdate = false;
            }

            _chunksNeedingLightUpdate.Remove(coord);
        }

        /// <summary>
        ///     Fills the provided list with all chunks in the RelightPending state
        ///     that do not have a light job in-flight.
        ///     Uses _relightPendingChunks secondary index — O(relight count) not O(all loaded).
        /// </summary>
        internal void FillChunksNeedingRelight(List<ManagedChunk> result)
        {
            result.Clear();

            foreach (ManagedChunk chunk in _relightPendingChunks)
            {
                if (!chunk.LightJobInFlight)
                {
                    result.Add(chunk);
                }
            }
        }

        /// <summary>
        ///     Removes a chunk from all secondary indices. Called during unload.
        /// </summary>
        internal void RemoveFromIndices(ManagedChunk chunk, int3 coord)
        {
            _generatedChunks.Remove(chunk);
            _readyChunks.Remove(chunk);
            _relightPendingChunks.Remove(chunk);
            _chunksNeedingLightUpdate.Remove(coord);
        }

        /// <summary>
        ///     Clears all secondary indices. Called by ChunkManager.Dispose().
        /// </summary>
        internal void Clear()
        {
            _chunksNeedingLightUpdate.Clear();
            _generatedChunks.Clear();
            _readyChunks.Clear();
            _relightPendingChunks.Clear();
        }
    }
}
