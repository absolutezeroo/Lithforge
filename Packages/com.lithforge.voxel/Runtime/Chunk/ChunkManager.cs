using System;
using System.Collections.Generic;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Storage;
using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Voxel.Chunk
{
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
        private readonly List<int> _neighborCountCache = new List<int>();
        private readonly List<int3> _toRemoveCache = new List<int3>();

        /// <summary>
        /// Cursor index into _loadQueue. Advanced instead of RemoveRange(0, count).
        /// Reset when UpdateLoadingQueue() rebuilds the queue.
        /// </summary>
        private int _loadQueueIndex;

        public int LoadedCount
        {
            get { return _chunks.Count; }
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

        public void UpdateLoadingQueue(int3 cameraChunkCoord)
        {
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
        }

        public void FillChunksToGenerate(List<ManagedChunk> result, int maxCount)
        {
            result.Clear();

            int remaining = _loadQueue.Count - _loadQueueIndex;
            int count = math.min(maxCount, remaining);

            for (int i = 0; i < count; i++)
            {
                int3 coord = _loadQueue[_loadQueueIndex + i];

                if (_chunks.ContainsKey(coord))
                {
                    continue;
                }

                NativeArray<StateId> data = _pool.Checkout();
                ManagedChunk chunk = new ManagedChunk(coord, data);
                chunk.State = ChunkState.Generating;
                _chunks[coord] = chunk;
                result.Add(chunk);
            }

            // Advance cursor instead of RemoveRange(0, count) — O(1) instead of O(n)
            _loadQueueIndex += count;

            // If cursor has consumed the entire queue, clear to free memory
            if (_loadQueueIndex >= _loadQueue.Count)
            {
                _loadQueue.Clear();
                _loadQueueIndex = 0;
            }
        }

        /// <summary>
        /// Fills the provided list with chunks in the Generated state, sorted by
        /// ready neighbor count (descending). Uses cached lists to avoid per-frame
        /// allocations. The caller must NOT store the result list reference beyond
        /// the current frame.
        /// </summary>
        public void FillChunksToMesh(List<ManagedChunk> result, int maxCount)
        {
            result.Clear();
            _meshCandidateCache.Clear();
            _neighborCountCache.Clear();

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                if (kvp.Value.State == ChunkState.Generated)
                {
                    _meshCandidateCache.Add(kvp.Value);
                    _neighborCountCache.Add(CountReadyNeighbors(kvp.Value.Coord));
                }
            }

            // Schwartzian transform: sort by pre-computed neighbor count (descending)
            // Simple insertion sort is fast for typical candidate counts (< 100)
            for (int i = 1; i < _meshCandidateCache.Count; i++)
            {
                ManagedChunk tempChunk = _meshCandidateCache[i];
                int tempCount = _neighborCountCache[i];
                int j = i - 1;

                while (j >= 0 && _neighborCountCache[j] < tempCount)
                {
                    _meshCandidateCache[j + 1] = _meshCandidateCache[j];
                    _neighborCountCache[j + 1] = _neighborCountCache[j];
                    j--;
                }

                _meshCandidateCache[j + 1] = tempChunk;
                _neighborCountCache[j + 1] = tempCount;
            }

            int resultCount = math.min(maxCount, _meshCandidateCache.Count);

            for (int i = 0; i < resultCount; i++)
            {
                result.Add(_meshCandidateCache[i]);
            }
        }

        /// <summary>
        /// Backward-compatible wrapper. Callers that cannot be updated yet
        /// may use this, but prefer FillChunksToMesh for zero-allocation usage.
        /// </summary>
        public List<ManagedChunk> GetChunksToMesh(int maxCount)
        {
            List<ManagedChunk> result = new List<ManagedChunk>();
            FillChunksToMesh(result, maxCount);

            return result;
        }

        private int CountReadyNeighbors(int3 coord)
        {
            int count = 0;

            if (HasGeneratedNeighbor(coord + new int3(1, 0, 0))) { count++; }
            if (HasGeneratedNeighbor(coord + new int3(-1, 0, 0))) { count++; }
            if (HasGeneratedNeighbor(coord + new int3(0, 1, 0))) { count++; }
            if (HasGeneratedNeighbor(coord + new int3(0, -1, 0))) { count++; }
            if (HasGeneratedNeighbor(coord + new int3(0, 0, 1))) { count++; }
            if (HasGeneratedNeighbor(coord + new int3(0, 0, -1))) { count++; }

            return count;
        }

        private bool HasGeneratedNeighbor(int3 coord)
        {
            return _chunks.TryGetValue(coord, out ManagedChunk neighbor)
                && neighbor.State >= ChunkState.RelightPending;
        }

        /// <summary>
        /// Fills the provided list with all chunks in the Ready state.
        /// Clears the list before filling. Used for LOD level assignment.
        /// </summary>
        public void FillReadyChunks(List<ManagedChunk> result)
        {
            result.Clear();

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                if (kvp.Value.State == ChunkState.Ready)
                {
                    result.Add(kvp.Value);
                }
            }
        }

        /// <summary>
        /// Fills the provided list with all chunks that need cross-chunk light updates.
        /// Clears the list before filling.
        /// </summary>
        public void FillChunksNeedingLightUpdate(List<ManagedChunk> result)
        {
            result.Clear();

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                if (kvp.Value.NeedsLightUpdate)
                {
                    result.Add(kvp.Value);
                }
            }
        }

        /// <summary>
        /// Fills the provided list with all chunks in the RelightPending state.
        /// Clears the list before filling.
        /// </summary>
        public void FillChunksNeedingRelight(List<ManagedChunk> result)
        {
            result.Clear();

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                if (kvp.Value.State == ChunkState.RelightPending)
                {
                    result.Add(kvp.Value);
                }
            }
        }

        public ManagedChunk GetChunk(int3 coord)
        {
            _chunks.TryGetValue(coord, out ManagedChunk chunk);

            return chunk;
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

            NativeArray<StateId> data = chunk.Data;
            data[index] = state;

            // Complete any running mesh job before resetting state
            if (chunk.State == ChunkState.Meshing)
            {
                chunk.ActiveJobHandle.Complete();
            }

            // Set to RelightPending — light must be recalculated before remeshing
            chunk.State = ChunkState.RelightPending;
            dirtiedChunks.Add(chunkCoord);

            // Border propagation: if local coord is 0 or 31, dirty the neighbor
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

            if (neighbor.State == ChunkState.Ready)
            {
                neighbor.State = ChunkState.Generated;
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

        public void UnloadDistantChunks(int3 cameraChunkCoord, List<int3> unloaded)
        {
            unloaded.Clear();
            _toRemoveCache.Clear();

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                int3 diff = kvp.Key - cameraChunkCoord;
                int xzDist = math.max(math.abs(diff.x), math.abs(diff.z));
                bool yOutOfRange = diff.y < _yUnloadMin || diff.y > _yUnloadMax;

                if (xzDist > _renderDistance + 1 || yOutOfRange)
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

                    _toRemoveCache.Add(kvp.Key);
                    unloaded.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _toRemoveCache.Count; i++)
            {
                _chunks.Remove(_toRemoveCache[i]);
            }
        }

        public void SaveAllChunks(WorldStorage storage)
        {
            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                ManagedChunk chunk = kvp.Value;

                if (chunk.State >= ChunkState.RelightPending && chunk.Data.IsCreated)
                {
                    chunk.ActiveJobHandle.Complete();
                    storage.SaveChunk(chunk.Coord, chunk.Data, chunk.LightData);
                }
            }
        }

        private static readonly int3[] _neighborOffsets = new int3[]
        {
            new int3(1, 0, 0),
            new int3(-1, 0, 0),
            new int3(0, 1, 0),
            new int3(0, -1, 0),
            new int3(0, 0, 1),
            new int3(0, 0, -1),
        };

        /// <summary>
        /// Marks all Ready neighbors of the given coord as Generated (needing remesh),
        /// and flags any Meshing neighbors for re-mesh after their current job completes.
        /// Called after a chunk finishes generation or is loaded from storage.
        /// </summary>
        public void InvalidateReadyNeighbors(int3 coord)
        {
            for (int i = 0; i < _neighborOffsets.Length; i++)
            {
                int3 neighborCoord = coord + _neighborOffsets[i];
                ManagedChunk neighbor = GetChunk(neighborCoord);

                if (neighbor == null)
                {
                    continue;
                }

                if (neighbor.State == ChunkState.Ready)
                {
                    neighbor.State = ChunkState.Generated;
                }
                else if (neighbor.State == ChunkState.Meshing)
                {
                    // Job is active — flag for re-mesh after it completes
                    neighbor.NeedsRemesh = true;
                }
            }
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
            }

            _chunks.Clear();
        }
    }
}
