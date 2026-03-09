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
        private readonly int _renderDistance;
        private readonly List<int3> _loadQueue = new List<int3>();
        private bool _disposed;

        public int LoadedCount
        {
            get { return _chunks.Count; }
        }

        public int PendingLoadCount
        {
            get { return _loadQueue.Count; }
        }

        public ChunkManager(ChunkPool pool, int renderDistance)
        {
            _pool = pool;
            _renderDistance = renderDistance;
        }

        public void UpdateLoadingQueue(int3 cameraChunkCoord)
        {
            _loadQueue.Clear();

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

                        for (int y = -1; y <= 3; y++)
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

        public List<ManagedChunk> GetChunksToGenerate(int maxCount)
        {
            List<ManagedChunk> result = new List<ManagedChunk>();

            int count = math.min(maxCount, _loadQueue.Count);

            for (int i = 0; i < count; i++)
            {
                int3 coord = _loadQueue[i];

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

            // Remove claimed coords from queue
            if (result.Count > 0)
            {
                for (int i = 0; i < result.Count; i++)
                {
                    _loadQueue.Remove(result[i].Coord);
                }
            }

            return result;
        }

        public List<ManagedChunk> GetChunksToMesh(int maxCount)
        {
            List<ManagedChunk> candidates = new List<ManagedChunk>();

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                if (kvp.Value.State == ChunkState.Generated)
                {
                    candidates.Add(kvp.Value);
                }
            }

            // Sort by ready neighbor count (descending) — chunks with more
            // generated neighbors produce fewer ghost faces and less re-meshing.
            candidates.Sort((a, b) =>
                CountReadyNeighbors(b.Coord).CompareTo(CountReadyNeighbors(a.Coord)));

            if (candidates.Count > maxCount)
            {
                candidates.RemoveRange(maxCount, candidates.Count - maxCount);
            }

            return candidates;
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
                && neighbor.State >= ChunkState.Generated;
        }

        public ManagedChunk GetChunk(int3 coord)
        {
            _chunks.TryGetValue(coord, out ManagedChunk chunk);

            return chunk;
        }

        public void UnloadDistantChunks(int3 cameraChunkCoord, List<int3> unloaded)
        {
            unloaded.Clear();
            List<int3> toRemove = new List<int3>();

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                int3 diff = kvp.Key - cameraChunkCoord;
                int xzDist = math.max(math.abs(diff.x), math.abs(diff.z));
                bool yOutOfRange = diff.y < -2 || diff.y > 4;

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

                    toRemove.Add(kvp.Key);
                    unloaded.Add(kvp.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                _chunks.Remove(toRemove[i]);
            }
        }

        public void SaveAllChunks(WorldStorage storage)
        {
            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                ManagedChunk chunk = kvp.Value;

                if (chunk.State >= ChunkState.Generated && chunk.Data.IsCreated)
                {
                    chunk.ActiveJobHandle.Complete();
                    storage.SaveChunk(chunk.Coord, chunk.Data, chunk.LightData);
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
