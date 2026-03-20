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
    ///     Unified persistence service that replaces both AsyncChunkSaver and GeneratedChunkCache.
    ///     Provides write coalescing via ConcurrentDictionary, an embedded LRU cache for pristine
    ///     chunks with eviction-triggered disk writes, and a dedicated I/O thread fed by a
    ///     ConcurrentQueue with ManualResetEventSlim signaling.
    /// </summary>
    public sealed class ChunkPersistenceService : IDisposable
    {
        /// <summary>Default LRU cache capacity for pristine chunks.</summary>
        private const int DefaultLruCapacity = 4096;

        /// <summary>Size in bytes of a voxel snapshot buffer (Volume * 2 bytes per StateId).</summary>
        private static readonly int s_voxelBufferSize = ChunkConstants.Volume * 2;

        /// <summary>Size in bytes of a light snapshot buffer (Volume * 1 byte per nibble-packed light).</summary>
        private static readonly int s_lightBufferSize = ChunkConstants.Volume;

        /// <summary>Pool of reusable byte arrays for voxel data snapshots.</summary>
        private readonly ConcurrentBag<byte[]> _voxelBufferPool = new();

        /// <summary>Pool of reusable byte arrays for light data snapshots.</summary>
        private readonly ConcurrentBag<byte[]> _lightBufferPool = new();

        /// <summary>
        ///     Write coalescing dictionary. When a coord is enqueued for save, its serialized
        ///     bytes are stored here. The I/O thread reads from here using the coord received
        ///     from the queue. If a second save for the same coord arrives before the first
        ///     is written, the bytes are replaced without adding a duplicate queue entry.
        /// </summary>
        private readonly ConcurrentDictionary<int3, byte[]> _pendingWrites = new();

        /// <summary>Thread-safe FIFO queue of coordinates pending disk write.</summary>
        private readonly ConcurrentQueue<int3> _writeQueue = new();

        /// <summary>Event signal that wakes the I/O thread when new work is enqueued.</summary>
        private readonly ManualResetEventSlim _workAvailable = new(false);

        /// <summary>Dedicated background thread draining the write queue.</summary>
        private readonly Thread _ioThread;

        /// <summary>World storage for writing serialized chunks to region files.</summary>
        private readonly WorldStorage _worldStorage;

        /// <summary>Logger for error reporting.</summary>
        private readonly ILogger _logger;

        /// <summary>Atomic count of pending writes (incremented on enqueue, decremented after disk write).</summary>
        private int _pendingCount;

        /// <summary>Whether this service has been disposed.</summary>
        private volatile bool _disposed;

        /// <summary>Volatile flag signaling the I/O thread to exit after draining remaining work.</summary>
        private volatile bool _shutdownRequested;

        // --- Embedded LRU cache for pristine (unmodified) chunks ---

        /// <summary>Lock protecting the LRU cache (main-thread only in practice, but safe for future use).</summary>
        private readonly object _lruLock = new();

        /// <summary>LRU lookup: chunk coordinate to linked list node.</summary>
        private readonly Dictionary<int3, LinkedListNode<LruEntry>> _lruLookup;

        /// <summary>LRU eviction order: MRU at back, LRU at front.</summary>
        private readonly LinkedList<LruEntry> _lruOrder = new();

        /// <summary>Maximum number of entries in the LRU cache.</summary>
        private readonly int _lruCapacity;

        /// <summary>Creates the persistence service and starts the I/O thread.</summary>
        public ChunkPersistenceService(
            WorldStorage worldStorage,
            ILogger logger = null,
            int lruCapacity = DefaultLruCapacity)
        {
            _worldStorage = worldStorage;
            _logger = logger;
            _lruCapacity = lruCapacity;
            _lruLookup = new Dictionary<int3, LinkedListNode<LruEntry>>(lruCapacity + 4);

            _ioThread = new Thread(IoLoop)
            {
                Name = "ChunkPersistence",
                IsBackground = true,
            };
            _ioThread.Start();
        }

        /// <summary>Number of entries currently in the LRU pristine cache.</summary>
        public int CacheCount
        {
            get
            {
                lock (_lruLock)
                {
                    return _lruLookup.Count;
                }
            }
        }

        /// <summary>Number of pending disk writes not yet processed by the I/O thread.</summary>
        public int PendingWriteCount
        {
            get { return Volatile.Read(ref _pendingCount); }
        }

        /// <summary>
        ///     Snapshots dirty chunk data to pooled byte buffers, serializes, and enqueues
        ///     a write to the I/O thread. Must be called BEFORE NativeArrays are disposed.
        /// </summary>
        public void EnqueueDirtySave(
            int3 coord,
            NativeArray<StateId> data,
            NativeArray<byte> lightData,
            Dictionary<int, IBlockEntity> blockEntities,
            float inhabitedTime)
        {
            byte[] voxelSnapshot = RentVoxelBuffer();

            for (int i = 0; i < data.Length; i++)
            {
                ushort val = data[i].Value;
                voxelSnapshot[i * 2] = (byte)(val & 0xFF);
                voxelSnapshot[i * 2 + 1] = (byte)(val >> 8);
            }

            byte[] lightSnapshot = null;

            if (lightData is { IsCreated: true, Length: > 0 })
            {
                lightSnapshot = RentLightBuffer();
                lightData.CopyTo(lightSnapshot);
            }

            // Shallow-copy to avoid concurrent mutation
            Dictionary<int, IBlockEntity> entitiesCopy = null;

            if (blockEntities is { Count: > 0 })
            {
                entitiesCopy = new Dictionary<int, IBlockEntity>(blockEntities);
            }

            byte[] serialized;

            try
            {
                serialized = ChunkSerializer.Serialize(
                    voxelSnapshot, ChunkConstants.Volume, lightSnapshot,
                    entitiesCopy, inhabitedTime);
            }
            finally
            {
                ReturnVoxelBuffer(voxelSnapshot);

                if (lightSnapshot is not null)
                {
                    ReturnLightBuffer(lightSnapshot);
                }
            }

            PushWrite(coord, serialized);
        }

        /// <summary>
        ///     Stores a pre-serialized pristine chunk in the LRU cache. When the cache
        ///     exceeds capacity, the least-recently-used entry is evicted to disk via the
        ///     I/O thread.
        /// </summary>
        public void CachePristineChunk(int3 coord, byte[] serializedData)
        {
            lock (_lruLock)
            {
                if (_lruLookup.TryGetValue(coord, out LinkedListNode<LruEntry> existing))
                {
                    existing.Value = new LruEntry { Coord = coord, Data = serializedData };
                    _lruOrder.Remove(existing);
                    _lruOrder.AddLast(existing);

                    return;
                }

                while (_lruLookup.Count >= _lruCapacity)
                {
                    EvictLru();
                }

                LinkedListNode<LruEntry> node = _lruOrder.AddLast(
                    new LruEntry { Coord = coord, Data = serializedData });
                _lruLookup[coord] = node;
            }
        }

        /// <summary>
        ///     Attempts to retrieve cached bytes for a chunk coordinate. Checks the write
        ///     coalescing dictionary first (read-your-writes), then the LRU cache.
        ///     Returns false if the chunk is not cached anywhere.
        /// </summary>
        public bool TryLoadCached(int3 coord, out byte[] data)
        {
            // Read-your-writes: check pending writes first
            if (_pendingWrites.TryGetValue(coord, out data))
            {
                return true;
            }

            lock (_lruLock)
            {
                if (_lruLookup.TryGetValue(coord, out LinkedListNode<LruEntry> node))
                {
                    _lruOrder.Remove(node);
                    _lruOrder.AddLast(node);
                    data = node.Value.Data;

                    return true;
                }
            }

            data = null;

            return false;
        }

        /// <summary>Removes a cached entry from the LRU cache.</summary>
        public void RemoveCached(int3 coord)
        {
            lock (_lruLock)
            {
                if (_lruLookup.TryGetValue(coord, out LinkedListNode<LruEntry> node))
                {
                    _lruLookup.Remove(coord);
                    _lruOrder.Remove(node);
                }
            }
        }

        /// <summary>
        ///     Drains the LRU cache to disk writes, then blocks until all pending writes
        ///     have been processed by the I/O thread. Also triggers an incremental flush
        ///     of region files. Used before shutdown and periodic auto-saves.
        /// </summary>
        public void Flush()
        {
            // Drain LRU cache to disk writes
            lock (_lruLock)
            {
                LinkedListNode<LruEntry> node = _lruOrder.First;

                while (node is not null)
                {
                    LinkedListNode<LruEntry> next = node.Next;
                    PushWrite(node.Value.Coord, node.Value.Data);
                    node = next;
                }

                _lruLookup.Clear();
                _lruOrder.Clear();
            }

            // Spin-wait for I/O thread to drain
            while (Volatile.Read(ref _pendingCount) > 0)
            {
                _workAvailable.Set();
                Thread.Sleep(1);
            }

            // Flush region files
            _worldStorage.FlushAllIncremental();
        }

        /// <summary>Signals the I/O thread to shut down and waits for it to finish draining.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _shutdownRequested = true;
            _workAvailable.Set();
            _ioThread.Join();
            _workAvailable.Dispose();
        }

        /// <summary>
        ///     Adds or replaces serialized bytes in the write coalescing dictionary and
        ///     enqueues the coordinate for the I/O thread. If the coord is already pending,
        ///     the bytes are replaced without adding a duplicate queue entry.
        /// </summary>
        private void PushWrite(int3 coord, byte[] serializedData)
        {
            bool isNew = _pendingWrites.TryAdd(coord, serializedData);

            if (isNew)
            {
                Interlocked.Increment(ref _pendingCount);
                _writeQueue.Enqueue(coord);
                _workAvailable.Set();
            }
            else
            {
                // Replace bytes — the queue already has this coord enqueued
                _pendingWrites[coord] = serializedData;
            }
        }

        /// <summary>Main loop of the I/O thread: waits for work, then drains the queue.</summary>
        private void IoLoop()
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

        /// <summary>Dequeues and writes all pending save requests to disk.</summary>
        private void DrainQueue()
        {
            while (_writeQueue.TryDequeue(out int3 coord))
            {
                if (_pendingWrites.TryRemove(coord, out byte[] data))
                {
                    try
                    {
                        _worldStorage.SaveChunkRaw(coord, data);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(
                            $"[ChunkPersistenceService] Save failed for {coord}: {ex.Message}");
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _pendingCount);
                    }
                }
            }
        }

        /// <summary>
        ///     Evicts the least-recently-used entry from the LRU cache and pushes
        ///     it to the I/O thread for disk write. Must be called under _lruLock.
        /// </summary>
        private void EvictLru()
        {
            LinkedListNode<LruEntry> lru = _lruOrder.First;

            if (lru is null)
            {
                return;
            }

            _lruLookup.Remove(lru.Value.Coord);
            _lruOrder.RemoveFirst();

            // Write evicted pristine chunk to disk
            PushWrite(lru.Value.Coord, lru.Value.Data);
        }

        /// <summary>Takes a voxel buffer from the pool, or allocates a new one.</summary>
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

        /// <summary>Takes a light buffer from the pool, or allocates a new one.</summary>
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

        /// <summary>LRU cache entry pairing a chunk coordinate with its serialized bytes.</summary>
        private struct LruEntry
        {
            /// <summary>Chunk coordinate key.</summary>
            public int3 Coord;

            /// <summary>Serialized chunk bytes.</summary>
            public byte[] Data;
        }
    }
}
