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
    /// <summary>
    ///     Background thread that serializes and saves dirty chunks to disk without
    ///     blocking the main thread. Chunk voxel/light data is snapshot-copied to pooled
    ///     byte arrays before enqueue so the NativeArrays can be recycled immediately.
    /// </summary>
    public sealed class AsyncChunkSaver : IDisposable
    {
        /// <summary>Size in bytes of a voxel snapshot buffer (Volume * 2 bytes per StateId).</summary>
        private static readonly int s_voxelBufferSize = ChunkConstants.Volume * 2;

        /// <summary>Size in bytes of a light snapshot buffer (Volume * 1 byte per nibble-packed light).</summary>
        private static readonly int s_lightBufferSize = ChunkConstants.Volume;

        /// <summary>Pool of reusable byte arrays for light data snapshots.</summary>
        private readonly ConcurrentBag<byte[]> _lightBufferPool = new();

        /// <summary>Logger for save error reporting.</summary>
        private readonly ILogger _logger;

        /// <summary>Thread-safe FIFO queue of pending save requests.</summary>
        private readonly ConcurrentQueue<SaveRequest> _queue = new();

        /// <summary>Pool of reusable byte arrays for voxel data snapshots.</summary>
        private readonly ConcurrentBag<byte[]> _voxelBufferPool = new();

        /// <summary>Event signal that wakes the worker thread when new work is enqueued.</summary>
        private readonly ManualResetEventSlim _workAvailable = new(false);

        /// <summary>Dedicated background thread that drains the save queue.</summary>
        private readonly Thread _workerThread;

        /// <summary>WorldStorage instance used to write serialized chunks to region files.</summary>
        private readonly WorldStorage _worldStorage;

        /// <summary>Whether this saver has been disposed.</summary>
        private bool _disposed;

        /// <summary>Atomic count of pending save requests (incremented before enqueue, decremented after save).</summary>
        private int _pendingCount;

        /// <summary>Volatile flag signaling the worker thread to exit after draining remaining work.</summary>
        private volatile bool _shutdownRequested;

        /// <summary>Creates an async saver and starts the background worker thread.</summary>
        public AsyncChunkSaver(WorldStorage worldStorage, ILogger logger = null)
        {
            _worldStorage = worldStorage;
            _logger = logger;
            _workerThread = new Thread(WorkerLoop)
            {
                Name = "ChunkSaver", IsBackground = true,
            };
            _workerThread.Start();
        }

        /// <summary>Signals the worker thread to shut down and waits for it to finish draining.</summary>
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

        /// <summary>
        ///     Copies chunk data to pooled byte[] buffers and enqueues a save request.
        ///     Must be called BEFORE the NativeArrays are disposed.
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

            if (lightData is
                {
                    IsCreated: true,
                    Length: > 0,
                })
            {
                lightSnapshot = RentLightBuffer();
                lightData.CopyTo(lightSnapshot);
            }

            // Shallow-copy to avoid concurrent mutation after caller returns
            Dictionary<int, IBlockEntity> entitiesCopy = null;

            if (blockEntities is
                {
                    Count: > 0,
                })
            {
                entitiesCopy = new Dictionary<int, IBlockEntity>(blockEntities);
            }

            // Increment BEFORE enqueue so Flush() never sees zero while items exist
            Interlocked.Increment(ref _pendingCount);

            _queue.Enqueue(new SaveRequest
            {
                Coord = coord, VoxelSnapshot = voxelSnapshot, LightSnapshot = lightSnapshot, BlockEntities = entitiesCopy,
            });

            _workAvailable.Set();
        }

        /// <summary>
        ///     Blocks until all enqueued save requests have been processed.
        ///     Called before FlushAll to ensure region caches are up to date.
        /// </summary>
        public void Flush()
        {
            while (Volatile.Read(ref _pendingCount) > 0)
            {
                _workAvailable.Set();
                Thread.Sleep(1);
            }
        }

        /// <summary>Main loop of the background thread: waits for work, then drains the queue.</summary>
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

        /// <summary>Dequeues and processes all pending save requests until the queue is empty.</summary>
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

        /// <summary>Takes a voxel buffer from the pool, or allocates a new one if the pool is empty.</summary>
        private byte[] RentVoxelBuffer()
        {
            if (_voxelBufferPool.TryTake(out byte[] buffer))
            {
                return buffer;
            }

            return new byte[s_voxelBufferSize];
        }

        /// <summary>Returns a voxel buffer to the pool for reuse.</summary>
        private void ReturnVoxelBuffer(byte[] buffer)
        {
            _voxelBufferPool.Add(buffer);
        }

        /// <summary>Takes a light buffer from the pool, or allocates a new one if the pool is empty.</summary>
        private byte[] RentLightBuffer()
        {
            if (_lightBufferPool.TryTake(out byte[] buffer))
            {
                return buffer;
            }

            return new byte[s_lightBufferSize];
        }

        /// <summary>Returns a light buffer to the pool for reuse.</summary>
        private void ReturnLightBuffer(byte[] buffer)
        {
            _lightBufferPool.Add(buffer);
        }

        /// <summary>Internal struct bundling a chunk coordinate with its data snapshots for the save queue.</summary>
        private struct SaveRequest
        {
            /// <summary>Chunk coordinate to save.</summary>
            public int3 Coord;

            /// <summary>Little-endian byte snapshot of voxel StateId values.</summary>
            public byte[] VoxelSnapshot;

            /// <summary>Nibble-packed light data snapshot, or null if no light data.</summary>
            public byte[] LightSnapshot;

            /// <summary>Shallow copy of block entities at enqueue time, or null if none.</summary>
            public Dictionary<int, IBlockEntity> BlockEntities;
        }
    }
}
