using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Lithforge.Core.Logging;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.BlockEntity;
using Lithforge.Voxel.Chunk;
using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Voxel.Storage
{
    public sealed class AsyncChunkSaver : IDisposable
    {
        private struct SaveRequest
        {
            public int3 Coord;
            public byte[] VoxelSnapshot;
            public byte[] LightSnapshot;
            public Dictionary<int, IBlockEntity> BlockEntities;
        }

        private readonly ConcurrentQueue<SaveRequest> _queue = new();
        private readonly WorldStorage _worldStorage;
        private readonly Thread _workerThread;
        private readonly ManualResetEventSlim _workAvailable = new(false);
        private readonly ILogger _logger;
        private volatile bool _shutdownRequested;
        private bool _disposed;
        private int _pendingCount;

        private readonly ConcurrentBag<byte[]> _voxelBufferPool = new();
        private readonly ConcurrentBag<byte[]> _lightBufferPool = new();

        private static readonly int s_voxelBufferSize = ChunkConstants.Volume * 2;
        private static readonly int s_lightBufferSize = ChunkConstants.Volume;

        public AsyncChunkSaver(WorldStorage worldStorage, ILogger logger = null)
        {
            _worldStorage = worldStorage;
            _logger = logger;
            _workerThread = new Thread(WorkerLoop)
            {
                Name = "ChunkSaver",
                IsBackground = true,
            };
            _workerThread.Start();
        }

        /// <summary>
        /// Copies chunk data to pooled byte[] buffers and enqueues a save request.
        /// Must be called BEFORE the NativeArrays are disposed.
        /// </summary>
        public void EnqueueSave(
            int3 coord,
            NativeArray<StateId> data,
            NativeArray<byte> lightData,
            Dictionary<int, IBlockEntity> blockEntities)
        {
            byte[] voxelSnapshot = RentVoxelBuffer();

            for (int i = 0; i < data.Length; i++)
            {
                ushort val = data[i].Value;
                voxelSnapshot[i * 2] = (byte)(val & 0xFF);
                voxelSnapshot[i * 2 + 1] = (byte)(val >> 8);
            }

            byte[] lightSnapshot = null;

            if (lightData.IsCreated && lightData.Length > 0)
            {
                lightSnapshot = RentLightBuffer();
                lightData.CopyTo(lightSnapshot);
            }

            // Shallow-copy to avoid concurrent mutation after caller returns
            Dictionary<int, IBlockEntity> entitiesCopy = null;

            if (blockEntities != null && blockEntities.Count > 0)
            {
                entitiesCopy = new Dictionary<int, IBlockEntity>(blockEntities);
            }

            // Increment BEFORE enqueue so Flush() never sees zero while items exist
            Interlocked.Increment(ref _pendingCount);

            _queue.Enqueue(new SaveRequest
            {
                Coord = coord,
                VoxelSnapshot = voxelSnapshot,
                LightSnapshot = lightSnapshot,
                BlockEntities = entitiesCopy,
            });

            _workAvailable.Set();
        }

        /// <summary>
        /// Blocks until all enqueued save requests have been processed.
        /// Called before FlushAll to ensure region caches are up to date.
        /// </summary>
        public void Flush()
        {
            while (Volatile.Read(ref _pendingCount) > 0)
            {
                _workAvailable.Set();
                Thread.Sleep(1);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _shutdownRequested = true;
            _workAvailable.Set();
            _workerThread.Join();
            _workAvailable.Dispose();
        }

        private void WorkerLoop()
        {
            while (!_shutdownRequested)
            {
                _workAvailable.Wait();
                _workAvailable.Reset();

                DrainQueue();
            }

            // Process any remaining items after shutdown signal
            DrainQueue();
        }

        private void DrainQueue()
        {
            while (_queue.TryDequeue(out SaveRequest request))
            {
                try
                {
                    byte[] serialized = ChunkSerializer.Serialize(
                        request.VoxelSnapshot,
                        ChunkConstants.Volume,
                        request.LightSnapshot,
                        request.BlockEntities);

                    _worldStorage.SaveChunkRaw(request.Coord, serialized);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(
                        $"[AsyncChunkSaver] Save failed for {request.Coord}: {ex.Message}");
                }
                finally
                {
                    ReturnVoxelBuffer(request.VoxelSnapshot);

                    if (request.LightSnapshot != null)
                    {
                        ReturnLightBuffer(request.LightSnapshot);
                    }

                    Interlocked.Decrement(ref _pendingCount);
                }
            }
        }

        private byte[] RentVoxelBuffer()
        {
            if (_voxelBufferPool.TryTake(out byte[] buffer))
            {
                return buffer;
            }

            return new byte[s_voxelBufferSize];
        }

        private void ReturnVoxelBuffer(byte[] buffer)
        {
            _voxelBufferPool.Add(buffer);
        }

        private byte[] RentLightBuffer()
        {
            if (_lightBufferPool.TryTake(out byte[] buffer))
            {
                return buffer;
            }

            return new byte[s_lightBufferSize];
        }

        private void ReturnLightBuffer(byte[] buffer)
        {
            _lightBufferPool.Add(buffer);
        }
    }
}
