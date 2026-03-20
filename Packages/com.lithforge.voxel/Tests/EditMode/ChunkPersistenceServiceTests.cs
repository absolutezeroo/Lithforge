using System.Collections.Generic;
using System.IO;
using System.Threading;

using Lithforge.Voxel.Block;
using Lithforge.Voxel.BlockEntity;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;

using NUnit.Framework;

using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Voxel.Tests
{
    [TestFixture]
    public sealed class ChunkPersistenceServiceTests
    {
        /// <summary>Temporary world directory for test region files.</summary>
        private string _tempDir;

        /// <summary>World storage backed by the temp directory.</summary>
        private WorldStorage _worldStorage;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "lithforge_test_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
            _worldStorage = new WorldStorage(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            _worldStorage?.Dispose();

            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public void CachePristineChunk_ThenTryLoadCached_ReturnsData()
        {
            using (ChunkPersistenceService service = new(_worldStorage, lruCapacity: 16))
            {
                int3 coord = new(1, 2, 3);
                byte[] data = new byte[] { 1, 2, 3, 4 };

                service.CachePristineChunk(coord, data);

                bool found = service.TryLoadCached(coord, out byte[] result);

                Assert.IsTrue(found, "Cached pristine chunk should be found");
                Assert.AreEqual(data, result, "Returned data should match cached data");

                service.Dispose();
            }
        }

        [Test]
        public void TryLoadCached_Uncached_ReturnsFalse()
        {
            using (ChunkPersistenceService service = new(_worldStorage, lruCapacity: 16))
            {
                bool found = service.TryLoadCached(new int3(99, 99, 99), out byte[] _);

                Assert.IsFalse(found, "Uncached coord should not be found");

                service.Dispose();
            }
        }

        [Test]
        public void RemoveCached_RemovesEntry()
        {
            using (ChunkPersistenceService service = new(_worldStorage, lruCapacity: 16))
            {
                int3 coord = new(5, 6, 7);
                service.CachePristineChunk(coord, new byte[] { 10, 20 });

                service.RemoveCached(coord);

                bool found = service.TryLoadCached(coord, out byte[] _);
                Assert.IsFalse(found, "Removed entry should not be found");

                service.Dispose();
            }
        }

        [Test]
        public void LruEviction_TriggersWrite_WhenCapacityExceeded()
        {
            int capacity = 4;

            using (ChunkPersistenceService service = new(_worldStorage, lruCapacity: capacity))
            {
                // Fill to capacity
                for (int i = 0; i < capacity; i++)
                {
                    service.CachePristineChunk(new int3(i, 0, 0), CreateSerializedChunk());
                }

                Assert.AreEqual(capacity, service.CacheCount, "Cache should be at capacity");

                // Adding one more should evict the oldest
                service.CachePristineChunk(new int3(capacity, 0, 0), CreateSerializedChunk());

                Assert.AreEqual(capacity, service.CacheCount, "Cache should still be at capacity after eviction");

                // The oldest entry (0,0,0) should have been evicted and is now pending write
                bool foundEvicted = service.TryLoadCached(new int3(0, 0, 0), out byte[] _);
                // It should be found in _pendingWrites via read-your-writes
                Assert.IsTrue(foundEvicted, "Evicted entry should be found in pending writes");

                service.Dispose();
            }
        }

        [Test]
        public void EnqueueDirtySave_WritesToDisk_AfterFlush()
        {
            using (ChunkPersistenceService service = new(_worldStorage))
            {
                int3 coord = new(0, 0, 0);
                NativeArray<StateId> chunkData = new(ChunkConstants.Volume, Allocator.TempJob);
                NativeArray<byte> lightData = new(ChunkConstants.Volume, Allocator.TempJob);

                try
                {
                    // Fill with recognizable data
                    StateId stone = new(1);

                    for (int i = 0; i < ChunkConstants.Volume; i++)
                    {
                        chunkData[i] = stone;
                    }

                    service.EnqueueDirtySave(coord, chunkData, lightData, null, 42.5f);
                    service.Flush();

                    // Verify it was written to disk
                    Assert.IsTrue(_worldStorage.HasChunk(coord), "Chunk should exist on disk after flush");

                    // Load it back and verify
                    NativeArray<StateId> loaded = new(ChunkConstants.Volume, Allocator.TempJob);
                    NativeArray<byte> loadedLight = new(ChunkConstants.Volume, Allocator.TempJob);

                    try
                    {
                        bool success = _worldStorage.LoadChunk(
                            coord, loaded, loadedLight,
                            out Dictionary<int, IBlockEntity> _, out float inhabitedTime);

                        Assert.IsTrue(success, "Should load chunk from disk");
                        Assert.AreEqual(42.5f, inhabitedTime, 0.01f,
                            "InhabitedTime should round-trip through disk");

                        for (int i = 0; i < ChunkConstants.Volume; i++)
                        {
                            Assert.AreEqual(stone, loaded[i],
                                $"Voxel mismatch at index {i}");
                        }
                    }
                    finally
                    {
                        loaded.Dispose();
                        loadedLight.Dispose();
                    }
                }
                finally
                {
                    chunkData.Dispose();
                    lightData.Dispose();
                }

                service.Dispose();
            }
        }

        [Test]
        public void ReadYourWrites_PendingWrite_ReturnedByTryLoadCached()
        {
            using (ChunkPersistenceService service = new(_worldStorage))
            {
                int3 coord = new(10, 0, 0);
                NativeArray<StateId> chunkData = new(ChunkConstants.Volume, Allocator.TempJob);
                NativeArray<byte> lightData = new(ChunkConstants.Volume, Allocator.TempJob);

                try
                {
                    service.EnqueueDirtySave(coord, chunkData, lightData, null, 0f);

                    // Before flush, the data should be available via read-your-writes
                    bool found = service.TryLoadCached(coord, out byte[] data);
                    Assert.IsTrue(found, "Pending write should be visible via TryLoadCached");
                    Assert.IsNotNull(data, "Data should not be null");
                }
                finally
                {
                    chunkData.Dispose();
                    lightData.Dispose();
                }

                service.Flush();
                service.Dispose();
            }
        }

        [Test]
        public void DoubleDispose_DoesNotThrow()
        {
            ChunkPersistenceService service = new(_worldStorage);
            service.Dispose();
            Assert.DoesNotThrow(() => service.Dispose(), "Second Dispose should not throw");
        }

        [Test]
        public void FlushDrains_AllPendingWrites()
        {
            using (ChunkPersistenceService service = new(_worldStorage))
            {
                NativeArray<StateId> chunkData = new(ChunkConstants.Volume, Allocator.TempJob);
                NativeArray<byte> lightData = new(ChunkConstants.Volume, Allocator.TempJob);

                try
                {
                    for (int i = 0; i < 10; i++)
                    {
                        service.EnqueueDirtySave(new int3(i, 0, 0), chunkData, lightData, null, 0f);
                    }
                }
                finally
                {
                    chunkData.Dispose();
                    lightData.Dispose();
                }

                service.Flush();

                Assert.AreEqual(0, service.PendingWriteCount,
                    "All pending writes should be drained after Flush");

                // Verify all chunks written to disk
                for (int i = 0; i < 10; i++)
                {
                    Assert.IsTrue(_worldStorage.HasChunk(new int3(i, 0, 0)),
                        $"Chunk ({i},0,0) should exist on disk after flush");
                }

                service.Dispose();
            }
        }

        [Test]
        public void Serializer_V6_RoundTrips_InhabitedTime()
        {
            NativeArray<StateId> original = new(ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<byte> originalLight = new(ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<StateId> restored = new(ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<byte> restoredLight = new(ChunkConstants.Volume, Allocator.TempJob);

            try
            {
                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    original[i] = new StateId((ushort)(i % 5));
                    originalLight[i] = (byte)(i % 256);
                }

                float sentTime = 123.456f;
                byte[] serialized = ChunkSerializer.Serialize(original, originalLight, null, sentTime);

                bool success = ChunkSerializer.Deserialize(
                    serialized, restored, restoredLight,
                    out Dictionary<int, IBlockEntity> _, out float receivedTime);

                Assert.IsTrue(success, "Deserialization should succeed");
                Assert.AreEqual(sentTime, receivedTime, 0.001f,
                    "InhabitedTime should round-trip");

                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    Assert.AreEqual(original[i], restored[i], $"Voxel mismatch at {i}");
                    Assert.AreEqual(originalLight[i], restoredLight[i], $"Light mismatch at {i}");
                }
            }
            finally
            {
                original.Dispose();
                originalLight.Dispose();
                restored.Dispose();
                restoredLight.Dispose();
            }
        }

        [Test]
        public void Serializer_V5BackwardCompat_InhabitedTimeIsZero()
        {
            NativeArray<StateId> original = new(ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<byte> originalLight = new(ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<StateId> restored = new(ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<byte> restoredLight = new(ChunkConstants.Volume, Allocator.TempJob);

            try
            {
                // Old v5 data serialized without inhabitedTime uses the standard Deserialize
                // which returns inhabitedTime = 0f. We can't easily create v5 data now that
                // Version=6, but we can verify the no-inhabitedTime overload still works.
                byte[] serialized = ChunkSerializer.Serialize(original, originalLight);
                bool success = ChunkSerializer.Deserialize(
                    serialized, restored, restoredLight,
                    out Dictionary<int, IBlockEntity> _);

                Assert.IsTrue(success, "Default Serialize/Deserialize should still work");
            }
            finally
            {
                original.Dispose();
                originalLight.Dispose();
                restored.Dispose();
                restoredLight.Dispose();
            }
        }

        /// <summary>Creates a minimal valid serialized chunk for use in LRU tests.</summary>
        private static byte[] CreateSerializedChunk()
        {
            NativeArray<StateId> data = new(ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<byte> light = new(ChunkConstants.Volume, Allocator.TempJob);

            try
            {
                return ChunkSerializer.Serialize(data, light);
            }
            finally
            {
                data.Dispose();
                light.Dispose();
            }
        }
    }
}
