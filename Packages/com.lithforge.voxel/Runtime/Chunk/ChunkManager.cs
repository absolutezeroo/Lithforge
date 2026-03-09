using System;
using System.Collections.Generic;
using Lithforge.Voxel.Block;
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
            List<ManagedChunk> result = new List<ManagedChunk>();

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                if (kvp.Value.State == ChunkState.Generated)
                {
                    result.Add(kvp.Value);

                    if (result.Count >= maxCount)
                    {
                        break;
                    }
                }
            }

            return result;
        }

        public ManagedChunk GetChunk(int3 coord)
        {
            ManagedChunk chunk;
            _chunks.TryGetValue(coord, out chunk);
            return chunk;
        }

        public void UnloadDistantChunks(int3 cameraChunkCoord, List<int3> unloaded)
        {
            unloaded.Clear();
            List<int3> toRemove = new List<int3>();

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                int3 diff = kvp.Key - cameraChunkCoord;
                int dist = math.max(math.abs(diff.x), math.abs(diff.z));

                if (dist > _renderDistance + 1)
                {
                    kvp.Value.ActiveJobHandle.Complete();

                    if (kvp.Value.Data.IsCreated)
                    {
                        _pool.Return(kvp.Value.Data);
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
            }

            _chunks.Clear();
        }
    }
}
