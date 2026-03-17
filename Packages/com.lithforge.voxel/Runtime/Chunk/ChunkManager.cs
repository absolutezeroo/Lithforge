using System;
using System.Collections.Generic;
using System.Diagnostics;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.BlockEntity;
using Lithforge.Voxel.Storage;
using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    /// Central authority for chunk lifecycle: loading, unloading, state transitions,
    /// block edits, and query methods consumed by schedulers. Owns the loaded chunk
    /// dictionary and secondary indices (_generatedChunks, _chunksNeedingLightUpdate).
    /// <remarks>
    /// All ChunkState transitions must go through <see cref="SetChunkState"/> so that
    /// secondary indices stay consistent. Direct assignment to chunk.State is forbidden
    /// outside this class.
    /// </remarks>
    /// </summary>
    public sealed class ChunkManager : IDisposable
    {
        private readonly Dictionary<int3, ManagedChunk> _chunks = new Dictionary<int3, ManagedChunk>();
        private readonly ChunkPool _pool;
        private int _renderDistance;
        private readonly int _yLoadMin;
        private readonly int _yLoadMax;
        private readonly int _yUnloadMin;
        private readonly int _yUnloadMax;
        private readonly List<int3> _loadQueue = new List<int3>();
        private bool _disposed;
        private readonly List<ManagedChunk> _meshCandidateCache = new List<ManagedChunk>();
        private readonly List<int> _meshScoreCache = new List<int>();
        private readonly List<int3> _toRemoveCache = new List<int3>();
        private readonly List<int3> _deferredDirtiedCache = new List<int3>();
        private readonly HashSet<int3> _chunksNeedingLightUpdate = new HashSet<int3>();
        private readonly HashSet<ManagedChunk> _generatedChunks = new HashSet<ManagedChunk>();
        private readonly HashSet<ManagedChunk> _readyChunks = new HashSet<ManagedChunk>();
        private readonly HashSet<ManagedChunk> _relightPendingChunks = new HashSet<ManagedChunk>();
        private int3 _lastCameraChunkCoord = new int3(int.MinValue, int.MinValue, int.MinValue);
        private AsyncChunkSaver _asyncSaver;

        /// <summary>
        /// Schwartzian transform caches for distance-sorted unload.
        /// Resized on demand, never shrunk. Owner: ChunkManager.
        /// </summary>
        private int[] _unloadDistCache = Array.Empty<int>();
        private int3[] _unloadCoordCache = Array.Empty<int3>();

        /// <summary>
        /// Called when a block with FlagHasBlockEntity is placed.
        /// Parameters: chunkCoord, flatIndex, stateId.
        /// </summary>
        public Action<int3, int, StateId> OnBlockEntityPlaced;

        /// <summary>
        /// Called when a block with FlagHasBlockEntity is broken (replaced by air).
        /// Parameters: chunkCoord, flatIndex, oldStateId.
        /// </summary>
        public Action<int3, int, StateId> OnBlockEntityRemoved;

        /// <summary>
        /// Called after any block change (both immediate and deferred paths).
        /// Parameters: worldCoord, newStateId.
        /// Used by <see cref="Network.ChunkDirtyTracker"/> for network delta sync.
        /// </summary>
        public Action<int3, StateId> OnBlockChanged;

        /// <summary>
        /// NativeStateRegistry reference for checking HasBlockEntity flag during SetBlock.
        /// Must be set after content pipeline completes.
        /// </summary>
        private NativeStateRegistry _nativeStateRegistry;

        /// <summary>
        /// Schwartzian transform caches for forward-weighted queue sort.
        /// Resized on demand, never shrunk. Owner: ChunkManager.
        /// </summary>
        private float[] _sortScoreCache = Array.Empty<float>();
        private int3[] _sortCoordCache = Array.Empty<int3>();

        /// <summary>
        /// Cursor index into _loadQueue. Advanced instead of RemoveRange(0, count).
        /// Reset when UpdateLoadingQueue() rebuilds the queue.
        /// </summary>
        private int _loadQueueIndex;

        public int LoadedCount
        {
            get { return _chunks.Count; }
        }

        public int GeneratedChunkCount
        {
            get { return _generatedChunks.Count; }
        }

        public int PendingLoadCount
        {
            get { return _loadQueue.Count - _loadQueueIndex; }
        }

        public int RenderDistance
        {
            get { return _renderDistance; }
        }

        public void SetRenderDistance(int distance)
        {
            _renderDistance = math.max(1, distance);
        }

        /// <summary>
        /// Centralized state transition. Maintains all secondary indices
        /// (_generatedChunks, _readyChunks, _relightPendingChunks).
        /// All external callers (schedulers) MUST use this instead of chunk.State = x.
        /// </summary>
        public void SetChunkState(ManagedChunk chunk, ChunkState newState)
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
        /// Called after any change to chunk.LightJobInFlight. Keeps _generatedChunks
        /// in sync: a Generated chunk with LightJobInFlight=true is NOT mesh-eligible.
        /// </summary>
        public void NotifyLightJobChanged(ManagedChunk chunk)
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
        /// Sets the NativeStateRegistry for block entity flag checks in SetBlock.
        /// Must be called after content pipeline completes and before gameplay starts.
        /// </summary>
        public void SetNativeStateRegistry(NativeStateRegistry nativeStateRegistry)
        {
            _nativeStateRegistry = nativeStateRegistry;
        }

        /// <summary>
        /// Sets the async chunk saver for off-thread serialization during unload.
        /// Must be called after construction and before gameplay starts.
        /// </summary>
        public void SetAsyncSaver(AsyncChunkSaver asyncSaver)
        {
            _asyncSaver = asyncSaver;
        }

        public ChunkManager(
            ChunkPool pool,
            int renderDistance,
            int yLoadMin = -1,
            int yLoadMax = 3,
            int yUnloadMin = -2,
            int yUnloadMax = 4)
        {
            _pool = pool;
            _renderDistance = renderDistance;
            _yLoadMin = yLoadMin;
            _yLoadMax = yLoadMax;
            _yUnloadMin = yUnloadMin;
            _yUnloadMax = yUnloadMax;
        }

        public void UpdateLoadingQueue(int3 cameraChunkCoord, float3 cameraForward)
        {
            int3 diff = cameraChunkCoord - _lastCameraChunkCoord;
            int movedDist = math.max(math.abs(diff.x), math.max(math.abs(diff.y), math.abs(diff.z)));

            // Only rebuild if camera moved far enough OR the queue is empty.
            // Prevents constant queue thrashing in fly mode.
            bool queueHasWork = _loadQueue.Count - _loadQueueIndex > 0;
            if (queueHasWork && movedDist < 2)
            {
                return;
            }

            _lastCameraChunkCoord = cameraChunkCoord;
            _loadQueue.Clear();
            _loadQueueIndex = 0;

            // Spiral order from center
            for (int d = 0; d <= _renderDistance; d++)
            {
                for (int x = -d; x <= d; x++)
                {
                    for (int z = -d; z <= d; z++)
                    {
                        if (math.abs(x) != d && math.abs(z) != d)
                        {
                            continue;
                        }

                        for (int y = _yLoadMin; y <= _yLoadMax; y++)
                        {
                            int3 coord = cameraChunkCoord + new int3(x, y, z);

                            if (!_chunks.ContainsKey(coord))
                            {
                                _loadQueue.Add(coord);
                            }
                        }
                    }
                }
            }

            // Schwartzian transform: pre-compute sort scores to avoid
            // repeated normalizesafe (rsqrt) inside O(n log n) comparisons.
            // score = dist² * (2 - dot), so forward chunks score lower (higher priority).
            int count = _loadQueue.Count;

            if (_sortScoreCache.Length < count)
            {
                int newSize = math.max(count, _sortScoreCache.Length * 2);
                _sortScoreCache = new float[newSize];
                _sortCoordCache = new int3[newSize];
            }

            float3 forwardXZ = math.normalizesafe(new float3(cameraForward.x, 0, cameraForward.z));

            for (int i = 0; i < count; i++)
            {
                int3 coord = _loadQueue[i];
                _sortCoordCache[i] = coord;

                int3 d = coord - cameraChunkCoord;
                float3 dirXZ = math.normalizesafe(new float3(d.x, 0, d.z));
                float dot = math.dot(dirXZ, forwardXZ);
                float distSq = math.lengthsq(d);
                _sortScoreCache[i] = distSq * (2.0f - dot);
            }

            Array.Sort(_sortScoreCache, _sortCoordCache, 0, count);

            _loadQueue.Clear();

            for (int i = 0; i < count; i++)
            {
                _loadQueue.Add(_sortCoordCache[i]);
            }
        }

        public void FillChunksToGenerate(List<ManagedChunk> result, int maxCount)
        {
            result.Clear();
            int created = 0;

            while (_loadQueueIndex < _loadQueue.Count && created < maxCount)
            {
                int3 coord = _loadQueue[_loadQueueIndex];
                _loadQueueIndex++;

                // Skip already-loaded chunks without consuming budget
                if (_chunks.ContainsKey(coord))
                {
                    continue;
                }

                NativeArray<StateId> data = _pool.Checkout();
                ManagedChunk chunk = new ManagedChunk(coord, data);
                _chunks[coord] = chunk;
                RegisterChunk(chunk);
                SetChunkState(chunk, ChunkState.Generating);
                result.Add(chunk);
                created++;
            }

            // If cursor has consumed the entire queue, clear to free memory
            if (_loadQueueIndex >= _loadQueue.Count)
            {
                _loadQueue.Clear();
                _loadQueueIndex = 0;
            }
        }

        /// <summary>
        /// Fills the provided list with Generated chunks, prioritized for meshing.
        /// Priority: neverMeshed (DESC) > hasAllNeighbors (DESC) > distScore (ASC).
        /// distScore uses Manhattan distance (no rsqrt). Neighbor readiness uses eagerly
        /// maintained ReadyNeighborMask bitmask (O(1) per chunk).
        /// Uses a single-pass top-K selection over _generatedChunks.
        /// </summary>
        public void FillChunksToMesh(
            List<ManagedChunk> result,
            int maxCount,
            int3 cameraChunkCoord,
            float3 cameraForwardXZ)
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

                int3 d = chunk.Coord - cameraChunkCoord;
                int distScore = math.abs(d.x) + math.abs(d.y) + math.abs(d.z);

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
        /// Returns true if buffer element at indexA is worse priority than element at indexB.
        /// Priority: hasPlayerEdit (DESC) > neverMeshed (DESC) > hasAllNeighbors (DESC) > distScore (ASC).
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
        /// Returns true if (hasEdit, neverMeshed, allNeighbors, distScore) is strictly better
        /// than (worstHasEdit, worstNeverMeshed, worstAllNeighbors, worstScore).
        /// Better = hasEdit first, then neverMeshed, then allNeighbors, then lower distScore.
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
        /// Fills with all chunks in Generated state.
        /// Used by LODScheduler to assign LOD levels before meshing.
        /// </summary>
        public void FillGeneratedChunks(List<ManagedChunk> result)
        {
            result.Clear();

            foreach (ManagedChunk chunk in _generatedChunks)
            {
                result.Add(chunk);
            }
        }

        /// <summary>
        /// Fills with Generated chunks that have LODLevel > 0.
        /// These need LOD meshing, not full-detail meshing.
        /// </summary>
        public void FillGeneratedChunksWithLOD(List<ManagedChunk> result)
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
        /// Fills the provided list with all chunks in the Ready state.
        /// Clears the list before filling. Used for LOD level assignment.
        /// Uses _readyChunks secondary index — O(ready count) not O(all loaded).
        /// </summary>
        public void FillReadyChunks(List<ManagedChunk> result)
        {
            result.Clear();

            foreach (ManagedChunk chunk in _readyChunks)
            {
                result.Add(chunk);
            }
        }

        /// <summary>
        /// Fills the provided list with all chunks that need cross-chunk light updates.
        /// Iterates only the dirty set instead of all loaded chunks — O(dirty) not O(all).
        /// Clears the list before filling.
        /// </summary>
        public void FillChunksNeedingLightUpdate(List<ManagedChunk> result)
        {
            result.Clear();

            foreach (int3 coord in _chunksNeedingLightUpdate)
            {
                if (_chunks.TryGetValue(coord, out ManagedChunk chunk) &&
                    chunk.NeedsLightUpdate && !chunk.LightJobInFlight)
                {
                    result.Add(chunk);
                }
            }
        }

        /// <summary>
        /// Marks a chunk as needing a cross-chunk light update and adds it to the dirty set.
        /// Call this instead of setting chunk.NeedsLightUpdate = true directly.
        /// </summary>
        public void MarkNeedsLightUpdate(int3 coord)
        {
            if (_chunks.TryGetValue(coord, out ManagedChunk chunk))
            {
                chunk.NeedsLightUpdate = true;
                _chunksNeedingLightUpdate.Add(coord);
            }
        }

        /// <summary>
        /// Clears the NeedsLightUpdate flag on a chunk and removes it from the dirty set.
        /// Call this instead of setting chunk.NeedsLightUpdate = false directly.
        /// </summary>
        public void ClearNeedsLightUpdate(int3 coord)
        {
            if (_chunks.TryGetValue(coord, out ManagedChunk chunk))
            {
                chunk.NeedsLightUpdate = false;
            }

            _chunksNeedingLightUpdate.Remove(coord);
        }

        /// <summary>
        /// Fills the provided list with all chunks in the RelightPending state
        /// that do not have a light job in-flight.
        /// Uses _relightPendingChunks secondary index — O(relight count) not O(all loaded).
        /// </summary>
        public void FillChunksNeedingRelight(List<ManagedChunk> result)
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

        public ManagedChunk GetChunk(int3 coord)
        {
            _chunks.TryGetValue(coord, out ManagedChunk chunk);

            return chunk;
        }

        /// <summary>
        /// Returns true if the block at the given world coordinate is in a loaded,
        /// sufficiently-generated chunk. Returns false if the chunk is missing,
        /// still generating, or has no data.
        /// </summary>
        public bool IsBlockLoaded(int3 worldCoord)
        {
            int3 chunkCoord = WorldToChunk(worldCoord);
            ManagedChunk chunk = GetChunk(chunkCoord);

            return chunk != null && chunk.State >= ChunkState.RelightPending && chunk.Data.IsCreated;
        }

        /// <summary>
        /// Gets the StateId at a world-space block coordinate.
        /// Returns StateId.Air if the chunk is not loaded or not yet generated.
        /// </summary>
        public StateId GetBlock(int3 worldCoord)
        {
            int3 chunkCoord = WorldToChunk(worldCoord);
            ManagedChunk chunk = GetChunk(chunkCoord);

            if (chunk == null || chunk.State < ChunkState.RelightPending || !chunk.Data.IsCreated)
            {
                return StateId.Air;
            }

            int localX = worldCoord.x - chunkCoord.x * ChunkConstants.Size;
            int localY = worldCoord.y - chunkCoord.y * ChunkConstants.Size;
            int localZ = worldCoord.z - chunkCoord.z * ChunkConstants.Size;
            int index = ChunkData.GetIndex(localX, localY, localZ);

            NativeArray<StateId> data = chunk.Data;

            return data[index];
        }

        /// <summary>
        /// Sets the StateId at a world-space block coordinate.
        /// Marks the chunk (and border-adjacent neighbors) for remeshing.
        /// Sets the chunk to RelightPending so light is recalculated before remesh.
        /// Does nothing if the chunk is not loaded or still generating.
        /// </summary>
        public void SetBlock(int3 worldCoord, StateId state, List<int3> dirtiedChunks)
        {
            int3 chunkCoord = WorldToChunk(worldCoord);
            ManagedChunk chunk = GetChunk(chunkCoord);

            if (chunk == null || chunk.State < ChunkState.RelightPending || !chunk.Data.IsCreated)
            {
                return;
            }

            int localX = worldCoord.x - chunkCoord.x * ChunkConstants.Size;
            int localY = worldCoord.y - chunkCoord.y * ChunkConstants.Size;
            int localZ = worldCoord.z - chunkCoord.z * ChunkConstants.Size;
            int index = ChunkData.GetIndex(localX, localY, localZ);

            chunk.IsDirty = true;
            chunk.HasPlayerEdit = true;

            // Check block entity flags for old and new states
            StateId oldState = chunk.Data[index];
            bool oldHasEntity = _nativeStateRegistry.States.IsCreated &&
                oldState.Value < _nativeStateRegistry.States.Length &&
                _nativeStateRegistry.States[oldState.Value].HasBlockEntity;
            bool newHasEntity = _nativeStateRegistry.States.IsCreated &&
                state.Value < _nativeStateRegistry.States.Length &&
                _nativeStateRegistry.States[state.Value].HasBlockEntity;

            if (chunk.State == ChunkState.Meshing)
            {
                // Defer the edit — do NOT write to ChunkData while the mesh job is
                // reading it. The edit will be applied in ApplyDeferredEdits() after
                // the job finishes. Block entity events are also deferred.
                chunk.DeferredEdits.Add(new DeferredEdit
                {
                    FlatIndex = index,
                    OldState = oldState,
                    NewState = state,
                });
            }
            else
            {
                // Normal path: write immediately and trigger relight
                NativeArray<StateId> data = chunk.Data;
                data[index] = state;
                chunk.PendingEditIndices.Add(index);
                SetChunkState(chunk, ChunkState.RelightPending);

                // Fire block entity events only on the immediate path
                if (oldHasEntity && !newHasEntity)
                {
                    OnBlockEntityRemoved?.Invoke(chunkCoord, index, oldState);
                }

                if (newHasEntity && !oldHasEntity)
                {
                    OnBlockEntityPlaced?.Invoke(chunkCoord, index, state);
                }

                OnBlockChanged?.Invoke(worldCoord, state);
            }

            dirtiedChunks.Add(chunkCoord);
            DirtyNeighborBorders(chunkCoord, localX, localY, localZ, dirtiedChunks);
        }

        /// <summary>
        /// Applies deferred edits that arrived while a chunk was in Meshing state.
        /// Writes the edits to ChunkData and fires block entity events for each edit.
        /// Called by MeshScheduler.PollCompleted after the mesh job finishes.
        /// </summary>
        public void ApplyDeferredEdits(ManagedChunk chunk)
        {
            NativeArray<StateId> chunkData = chunk.Data;
            _deferredDirtiedCache.Clear();
            _deferredDirtiedCache.Add(chunk.Coord);

            for (int di = 0; di < chunk.DeferredEdits.Count; di++)
            {
                DeferredEdit edit = chunk.DeferredEdits[di];
                chunkData[edit.FlatIndex] = edit.NewState;
                chunk.PendingEditIndices.Add(edit.FlatIndex);

                // Unpack flat index to local coordinates
                int localY = edit.FlatIndex / ChunkConstants.SizeSquared;
                int remainder = edit.FlatIndex % ChunkConstants.SizeSquared;
                int localZ = remainder / ChunkConstants.Size;
                int localX = remainder % ChunkConstants.Size;

                // Fire block entity events now that the voxel write has happened
                bool editOldHasEntity = _nativeStateRegistry.States.IsCreated &&
                    edit.OldState.Value < _nativeStateRegistry.States.Length &&
                    _nativeStateRegistry.States[edit.OldState.Value].HasBlockEntity;
                bool editNewHasEntity = _nativeStateRegistry.States.IsCreated &&
                    edit.NewState.Value < _nativeStateRegistry.States.Length &&
                    _nativeStateRegistry.States[edit.NewState.Value].HasBlockEntity;

                if (editOldHasEntity && !editNewHasEntity)
                {
                    OnBlockEntityRemoved?.Invoke(chunk.Coord, edit.FlatIndex, edit.OldState);
                }

                if (editNewHasEntity && !editOldHasEntity)
                {
                    OnBlockEntityPlaced?.Invoke(chunk.Coord, edit.FlatIndex, edit.NewState);
                }

                // Fire network dirty tracking event
                if (OnBlockChanged != null)
                {
                    int3 worldCoord = new int3(
                        chunk.Coord.x * ChunkConstants.Size + localX,
                        chunk.Coord.y * ChunkConstants.Size + localY,
                        chunk.Coord.z * ChunkConstants.Size + localZ);
                    OnBlockChanged.Invoke(worldCoord, edit.NewState);
                }

                // Dirty neighbor chunks for border-touching edits
                DirtyNeighborBorders(chunk.Coord, localX, localY, localZ, _deferredDirtiedCache);
            }

            chunk.DeferredEdits.Clear();
            SetChunkState(chunk, ChunkState.RelightPending);
        }

        /// <summary>
        /// Dirties neighbor chunks when an edit is on a chunk border face (local coord 0 or 31).
        /// Extracted to avoid duplication between normal and deferred edit paths.
        /// </summary>
        private void DirtyNeighborBorders(int3 chunkCoord, int localX, int localY, int localZ,
            List<int3> dirtiedChunks)
        {
            if (localX == 0)
            {
                DirtyNeighborChunk(chunkCoord + new int3(-1, 0, 0), dirtiedChunks);
            }

            if (localX == ChunkConstants.SizeMask)
            {
                DirtyNeighborChunk(chunkCoord + new int3(1, 0, 0), dirtiedChunks);
            }

            if (localY == 0)
            {
                DirtyNeighborChunk(chunkCoord + new int3(0, -1, 0), dirtiedChunks);
            }

            if (localY == ChunkConstants.SizeMask)
            {
                DirtyNeighborChunk(chunkCoord + new int3(0, 1, 0), dirtiedChunks);
            }

            if (localZ == 0)
            {
                DirtyNeighborChunk(chunkCoord + new int3(0, 0, -1), dirtiedChunks);
            }

            if (localZ == ChunkConstants.SizeMask)
            {
                DirtyNeighborChunk(chunkCoord + new int3(0, 0, 1), dirtiedChunks);
            }
        }

        private void DirtyNeighborChunk(int3 neighborCoord, List<int3> dirtiedChunks)
        {
            ManagedChunk neighbor = GetChunk(neighborCoord);

            if (neighbor == null || neighbor.State < ChunkState.RelightPending)
            {
                return;
            }

            neighbor.HasPlayerEdit = true;

            if (neighbor.State == ChunkState.Ready)
            {
                SetChunkState(neighbor, ChunkState.Generated);
                dirtiedChunks.Add(neighborCoord);
            }
            else if (neighbor.State == ChunkState.Meshing)
            {
                neighbor.NeedsRemesh = true;
            }
        }

        /// <summary>
        /// Converts a world-space block coordinate to a chunk coordinate.
        /// Uses floor division to handle negative coordinates correctly.
        /// </summary>
        public static int3 WorldToChunk(int3 worldCoord)
        {
            return new int3(
                FloorDiv(worldCoord.x, ChunkConstants.Size),
                FloorDiv(worldCoord.y, ChunkConstants.Size),
                FloorDiv(worldCoord.z, ChunkConstants.Size));
        }

        private static int FloorDiv(int a, int b)
        {
            return a >= 0 ? a / b : (a - b + 1) / b;
        }

        public void UnloadDistantChunks(
            int3 cameraChunkCoord,
            List<int3> unloaded,
            WorldStorage worldStorage = null,
            float budgetMs = 2.0f)
        {
            unloaded.Clear();
            _toRemoveCache.Clear();

            // Phase 1: Collect all out-of-range candidates (distance test only)
            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                int3 diff = kvp.Key - cameraChunkCoord;
                int xzDist = math.max(math.abs(diff.x), math.abs(diff.z));
                bool yOutOfRange = diff.y < _yUnloadMin || diff.y > _yUnloadMax;

                if (xzDist > _renderDistance + 1 || yOutOfRange)
                {
                    _toRemoveCache.Add(kvp.Key);
                }
            }

            int candidateCount = _toRemoveCache.Count;

            if (candidateCount == 0)
            {
                return;
            }

            // Phase 2: Schwartzian sort by Chebyshev XZ distance (ascending)
            if (_unloadDistCache.Length < candidateCount)
            {
                int newSize = math.max(candidateCount, _unloadDistCache.Length * 2);
                _unloadDistCache = new int[newSize];
                _unloadCoordCache = new int3[newSize];
            }

            for (int i = 0; i < candidateCount; i++)
            {
                int3 coord = _toRemoveCache[i];
                _unloadCoordCache[i] = coord;
                int3 diff = coord - cameraChunkCoord;
                _unloadDistCache[i] = math.max(math.abs(diff.x), math.abs(diff.z));
            }

            Array.Sort(_unloadDistCache, _unloadCoordCache, 0, candidateCount);

            // Phase 3: Process within budget, farthest first (iterate in reverse)
            long startTicks = Stopwatch.GetTimestamp();
            double ticksPerMs = Stopwatch.Frequency / 1000.0;

            for (int i = candidateCount - 1; i >= 0; i--)
            {
                int3 coord = _unloadCoordCache[i];
                ManagedChunk chunk = _chunks[coord];

                chunk.ActiveJobHandle.Complete();

                // Notify block entities of unload
                if (chunk.BlockEntities != null)
                {
                    foreach (KeyValuePair<int, IBlockEntity> bePair in chunk.BlockEntities)
                    {
                        bePair.Value.OnChunkUnload();
                    }
                }

                // Save modified chunks before unloading (enqueue must precede dispose)
                if (chunk.IsDirty && chunk.Data.IsCreated)
                {
                    if (_asyncSaver != null)
                    {
                        _asyncSaver.EnqueueSave(
                            chunk.Coord, chunk.Data, chunk.LightData, chunk.BlockEntities);
                    }
                    else if (worldStorage != null)
                    {
                        worldStorage.SaveChunk(
                            chunk.Coord, chunk.Data, chunk.LightData, chunk.BlockEntities);
                    }

                    chunk.IsDirty = false;
                }

                if (chunk.Data.IsCreated)
                {
                    _pool.Return(chunk.Data);
                }

                if (chunk.LightData.IsCreated)
                {
                    chunk.LightData.Dispose();
                }

                if (chunk.HeightMap.IsCreated)
                {
                    chunk.HeightMap.Dispose();
                }

                if (chunk.RiverFlags.IsCreated)
                {
                    chunk.RiverFlags.Dispose();
                }

                unloaded.Add(coord);

                // Check budget
                double elapsedMs = (Stopwatch.GetTimestamp() - startTicks) / ticksPerMs;

                if (elapsedMs >= budgetMs)
                {
                    break;
                }
            }

            // Phase 4: Remove processed chunks from _chunks and index sets
            for (int i = 0; i < unloaded.Count; i++)
            {
                if (_chunks.TryGetValue(unloaded[i], out ManagedChunk unloadedChunk))
                {
                    UnregisterChunk(unloadedChunk);
                    _generatedChunks.Remove(unloadedChunk);
                    _readyChunks.Remove(unloadedChunk);
                    _relightPendingChunks.Remove(unloadedChunk);
                }

                _chunksNeedingLightUpdate.Remove(unloaded[i]);
                _chunks.Remove(unloaded[i]);
            }
        }

        /// <summary>
        /// Fills the provided list with dirty chunks eligible for saving.
        /// A chunk is eligible if IsDirty, State >= RelightPending, and Data.IsCreated.
        /// Clears the list before filling. Caller uses fill pattern.
        /// </summary>
        public void CollectDirtyChunks(List<ManagedChunk> result)
        {
            result.Clear();

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                ManagedChunk chunk = kvp.Value;

                if (chunk.IsDirty && chunk.State >= ChunkState.RelightPending && chunk.Data.IsCreated)
                {
                    result.Add(chunk);
                }
            }
        }

        public void SaveAllChunks(WorldStorage storage)
        {
            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                ManagedChunk chunk = kvp.Value;

                if (chunk.IsDirty && chunk.State >= ChunkState.RelightPending && chunk.Data.IsCreated)
                {
                    chunk.ActiveJobHandle.Complete();
                    storage.SaveChunk(chunk.Coord, chunk.Data, chunk.LightData, chunk.BlockEntities);
                    chunk.IsDirty = false;
                }
            }
        }

        /// <summary>
        /// Face-indexed neighbor offsets: 0=+X, 1=-X, 2=+Y, 3=-Y, 4=+Z, 5=-Z.
        /// </summary>
        public static readonly int3[] FaceOffsets = new int3[]
        {
            new int3(1, 0, 0),   // 0: +X
            new int3(-1, 0, 0),  // 1: -X
            new int3(0, 1, 0),   // 2: +Y
            new int3(0, -1, 0),  // 3: -Y
            new int3(0, 0, 1),   // 4: +Z
            new int3(0, 0, -1),  // 5: -Z
        };

        private static readonly int3[] s_neighborOffsets = FaceOffsets;

        /// <summary>
        /// Maps face index to its opposite: +X(0)↔-X(1), +Y(2)↔-Y(3), +Z(4)↔-Z(5).
        /// Used by RegisterChunk, UnregisterChunk, and SetChunkState for bitmask updates.
        /// </summary>
        private static readonly int[] s_oppositeFace = { 1, 0, 3, 2, 5, 4 };

        /// <summary>
        /// Marks all Ready neighbors of the given coord as Generated (needing remesh),
        /// and flags any Meshing neighbors for re-mesh after their current job completes.
        /// Called after a chunk finishes generation or is loaded from storage.
        /// </summary>
        /// <summary>
        /// Fills the given array with chunk counts per state. The array must have
        /// length >= ChunkState enum count (8). Also outputs NeedsRemesh and
        /// NeedsLightUpdate totals. Single iteration over _chunks.
        /// </summary>
        public void FillStateHistogram(int[] countsByState, out int needsRemesh, out int needsLightUpdate)
        {
            for (int i = 0; i < countsByState.Length; i++)
            {
                countsByState[i] = 0;
            }

            needsRemesh = 0;
            needsLightUpdate = 0;

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                ManagedChunk chunk = kvp.Value;
                int stateIdx = (int)chunk.State;

                if (stateIdx >= 0 && stateIdx < countsByState.Length)
                {
                    countsByState[stateIdx]++;
                }

                if (chunk.NeedsRemesh)
                {
                    needsRemesh++;
                }

                if (chunk.NeedsLightUpdate)
                {
                    needsLightUpdate++;
                }
            }
        }

        public void InvalidateReadyNeighbors(int3 coord)
        {
            ManagedChunk chunk = GetChunk(coord);

            if (chunk == null)
            {
                return;
            }

            for (int f = 0; f < 6; f++)
            {
                ManagedChunk neighbor = chunk.Neighbors[f];

                if (neighbor == null)
                {
                    continue;
                }

                if (neighbor.State == ChunkState.Ready)
                {
                    SetChunkState(neighbor, ChunkState.Generated);
                }
                else if (neighbor.State == ChunkState.Meshing)
                {
                    // Job is active — flag for re-mesh after it completes
                    neighbor.NeedsRemesh = true;
                }
            }
        }

        /// <summary>
        /// Wires bidirectional Neighbors references and initializes ReadyNeighborMask
        /// for a newly created chunk. Pre-sets bits for Y-boundary faces so that
        /// world-edge chunks pass the "all neighbors" gate with fewer actual neighbors.
        /// Must be called after the chunk is inserted into _chunks.
        /// </summary>
        private void RegisterChunk(ManagedChunk chunk)
        {
            // Pre-set bits for Y-boundary faces that will never have a neighbor.
            byte boundaryMask = 0;

            if (chunk.Coord.y <= _yLoadMin)
            {
                boundaryMask |= (1 << 3); // face 3 = -Y: no chunk below world range
            }

            if (chunk.Coord.y >= _yLoadMax)
            {
                boundaryMask |= (1 << 2); // face 2 = +Y: no chunk above world range
            }

            chunk.ReadyNeighborMask = boundaryMask;

            for (int f = 0; f < 6; f++)
            {
                int3 neighborCoord = chunk.Coord + s_neighborOffsets[f];

                if (!_chunks.TryGetValue(neighborCoord, out ManagedChunk neighbor))
                {
                    continue;
                }

                // Wire new chunk -> existing neighbor
                chunk.Neighbors[f] = neighbor;

                if (neighbor.State >= ChunkState.RelightPending)
                {
                    chunk.ReadyNeighborMask |= (byte)(1 << f);
                }

                // Wire existing neighbor -> new chunk (opposite face)
                int oppF = s_oppositeFace[f];
                neighbor.Neighbors[oppF] = chunk;

                // New chunk starts at Generating (< RelightPending), so don't set
                // the neighbor's bitmask bit yet — it will be set when this chunk
                // transitions to >= RelightPending via SetChunkState.
            }
        }

        /// <summary>
        /// Clears bidirectional Neighbors references for a chunk being unloaded.
        /// Clears the corresponding ReadyNeighborMask bit on each neighbor.
        /// Must be called before the chunk is removed from _chunks.
        /// </summary>
        private void UnregisterChunk(ManagedChunk chunk)
        {
            for (int f = 0; f < 6; f++)
            {
                ManagedChunk neighbor = chunk.Neighbors[f];

                if (neighbor == null)
                {
                    continue;
                }

                int oppF = s_oppositeFace[f];
                neighbor.ReadyNeighborMask &= (byte)~(1 << oppF);
                neighbor.Neighbors[oppF] = null;
                chunk.Neighbors[f] = null;
            }
        }

        /// <summary>
        /// Rebuilds the BorderFaceMask bitmask from the chunk's current BorderLightEntries.
        /// Must be called after any modification to BorderLightEntries.
        /// </summary>
        public static void RebuildBorderFaceMask(ManagedChunk chunk)
        {
            byte mask = 0;

            for (int i = 0; i < chunk.BorderLightEntries.Count; i++)
            {
                mask |= (byte)(1 << chunk.BorderLightEntries[i].Face);
            }

            chunk.BorderFaceMask = mask;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                kvp.Value.ActiveJobHandle.Complete();

                if (kvp.Value.Data.IsCreated)
                {
                    _pool.Return(kvp.Value.Data);
                }

                if (kvp.Value.LightData.IsCreated)
                {
                    kvp.Value.LightData.Dispose();
                }

                if (kvp.Value.HeightMap.IsCreated)
                {
                    kvp.Value.HeightMap.Dispose();
                }

                if (kvp.Value.RiverFlags.IsCreated)
                {
                    kvp.Value.RiverFlags.Dispose();
                }
            }

            _chunks.Clear();
            _chunksNeedingLightUpdate.Clear();
            _generatedChunks.Clear();
            _readyChunks.Clear();
            _relightPendingChunks.Clear();
        }

        /// <summary>
        /// Returns the chunk at the given coordinate, or null if not loaded.
        /// Alias for <see cref="GetChunk"/> for clarity in nullable-context usage.
        /// </summary>
        public ManagedChunk TryGetChunk(int3 coord)
        {
            _chunks.TryGetValue(coord, out ManagedChunk chunk);

            return chunk;
        }

        /// <summary>
        /// Returns the last camera chunk coordinate used by <see cref="UpdateLoadingQueue"/>.
        /// </summary>
        public int3 GetCameraChunkCoord()
        {
            return _lastCameraChunkCoord;
        }

        /// <summary>
        /// Fills the result list with chunks that have allocated LiquidData and are
        /// within the given radius from the camera chunk. Only includes chunks in
        /// Generated or Ready state.
        /// </summary>
        public void FillChunksWithLiquid(List<ManagedChunk> result, int3 cameraChunkCoord, int radius)
        {
            result.Clear();

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                ManagedChunk chunk = kvp.Value;

                if (!chunk.LiquidData.IsCreated)
                {
                    continue;
                }

                if (chunk.State < ChunkState.Generated)
                {
                    continue;
                }

                int3 diff = chunk.Coord - cameraChunkCoord;
                int chebyshev = math.max(math.abs(diff.x), math.max(math.abs(diff.y), math.abs(diff.z)));

                if (chebyshev <= radius)
                {
                    result.Add(chunk);
                }
            }
        }

        /// <summary>
        /// Returns the liquid cell byte at a world-space block coordinate.
        /// Returns 0 (empty) if the chunk is not loaded or has no liquid data.
        /// </summary>
        public byte GetFluidLevel(int3 worldCoord)
        {
            int3 chunkCoord = WorldToChunk(worldCoord);
            ManagedChunk chunk = GetChunk(chunkCoord);

            if (chunk == null || !chunk.LiquidData.IsCreated)
            {
                return 0;
            }

            // Complete any in-flight liquid job before reading LiquidData.
            // PlayerPhysicsBody ticks before LiquidScheduler.PollCompleted().
            chunk.LiquidJobHandle.Complete();

            int localX = worldCoord.x - chunkCoord.x * ChunkConstants.Size;
            int localY = worldCoord.y - chunkCoord.y * ChunkConstants.Size;
            int localZ = worldCoord.z - chunkCoord.z * ChunkConstants.Size;
            int index = ChunkData.GetIndex(localX, localY, localZ);

            return chunk.LiquidData[index];
        }
    }
}
