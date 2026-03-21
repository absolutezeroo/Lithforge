using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using NUnit.Framework;

using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Voxel.Tests
{
    /// <summary>Tests for <see cref="ChunkLoadQueue"/> loading queue management, sorting, and reference counting.</summary>
    [TestFixture]
    public sealed class ChunkLoadQueueTests
    {
        /// <summary>Pool for allocating chunk NativeArrays.</summary>
        private ChunkPool _pool;

        /// <summary>Tracks all created chunks for cleanup.</summary>
        private List<ManagedChunk> _createdChunks;

        /// <summary>Allocates pool and scratch lists.</summary>
        [SetUp]
        public void SetUp()
        {
            _pool = new ChunkPool(128);
            _createdChunks = new List<ManagedChunk>();
        }

        /// <summary>Returns all checked-out arrays and disposes the pool.</summary>
        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdChunks.Count; i++)
            {
                if (_createdChunks[i].Data.IsCreated)
                {
                    _pool.Return(_createdChunks[i].Data);
                    _createdChunks[i].Data = default;
                }
            }

            _pool.Dispose();
        }

        /// <summary>UpdateLoadingQueue with RD=1 and empty chunks generates the correct number of pending loads.</summary>
        [Test]
        public void UpdateLoadingQueue_GeneratesCorrectCountForRD1()
        {
            ChunkLoadQueue queue = new(1, -1, 3, -2, 4);
            ConcurrentDictionary<int3, ManagedChunk> chunks = new();

            queue.UpdateLoadingQueue(int3.zero, new float3(0, 0, 1), chunks);

            // RD=1: 3x3 XZ grid (distance 0 + shell at d=1) × 5 Y levels (-1..3)
            Assert.AreEqual(45, queue.PendingLoadCount);
        }

        /// <summary>UpdateLoadingQueue produces no duplicate coordinates.</summary>
        [Test]
        public void UpdateLoadingQueue_NoDuplicates()
        {
            ChunkLoadQueue queue = new(2, -1, 3, -2, 4);
            ConcurrentDictionary<int3, ManagedChunk> chunks = new();

            queue.UpdateLoadingQueue(int3.zero, new float3(0, 0, 1), chunks);

            // Drain the queue into a set to check for duplicates
            List<int3> result = new();
            int totalCount = queue.PendingLoadCount;
            queue.FillCoordsToGenerate(result, totalCount, chunks);

            HashSet<int3> unique = new();

            for (int i = 0; i < result.Count; i++)
            {
                bool added = unique.Add(result[i]);
                Assert.IsTrue(added, $"Duplicate coordinate found: {result[i]}");
            }
        }

        /// <summary>UpdateLoadingQueue sorts by forward-weighted distance; closest forward chunk is first.</summary>
        [Test]
        public void UpdateLoadingQueue_SortsByForwardWeightedDistance()
        {
            ChunkLoadQueue queue = new(3, 0, 0, -1, 1);
            ConcurrentDictionary<int3, ManagedChunk> chunks = new();

            queue.UpdateLoadingQueue(int3.zero, new float3(0, 0, 1), chunks);

            List<int3> result = new();
            queue.FillCoordsToGenerate(result, 1, chunks);

            Assert.AreEqual(1, result.Count);

            // The first dequeued coord should be the origin itself (distance 0)
            // or (0,0,1) since forward is +Z — both have very low forward-weighted score.
            int3 first = result[0];
            int dist = math.abs(first.x) + math.abs(first.y) + math.abs(first.z);
            Assert.LessOrEqual(dist, 1, $"First dequeued chunk {first} should be very close to origin");
        }

        /// <summary>UpdateLoadingQueue skips chunks already present in the loaded dictionary.</summary>
        [Test]
        public void UpdateLoadingQueue_SkipsAlreadyLoadedChunks()
        {
            ChunkLoadQueue queue = new(1, 0, 0, -1, 1);
            ConcurrentDictionary<int3, ManagedChunk> chunks = new();

            // Pre-load the origin chunk
            ManagedChunk existing = CreateChunk(int3.zero);
            chunks[int3.zero] = existing;

            queue.UpdateLoadingQueue(int3.zero, new float3(0, 0, 1), chunks);

            // Drain all and check none is the origin
            List<int3> result = new();
            queue.FillCoordsToGenerate(result, 100, chunks);

            for (int i = 0; i < result.Count; i++)
            {
                Assert.AreNotEqual(int3.zero, result[i], "Origin chunk should be skipped since it's already loaded");
            }
        }

        /// <summary>Two players at distance 4 produce the union of both interest regions.</summary>
        [Test]
        public void UpdateLoadingQueue_MultiPlayer_UnionOfRegions()
        {
            ChunkLoadQueue queue = new(1, 0, 0, -1, 1);
            ConcurrentDictionary<int3, ManagedChunk> chunks = new();

            Span<int3> players = stackalloc int3[2];
            players[0] = int3.zero;
            players[1] = new int3(4, 0, 0);

            queue.UpdateLoadingQueue(players, new float3(0, 0, 1), chunks);

            // Each player has a 3×3 XZ grid × 1 Y level = 9 chunks
            // At distance 4, the two 3x3 regions don't overlap, so 18 total
            Assert.AreEqual(18, queue.PendingLoadCount);
        }

        /// <summary>AdjustRefCounts sets RefCount=0 and arms grace period when chunk leaves player range.</summary>
        [Test]
        public void AdjustRefCounts_ZeroRefArmsGracePeriod()
        {
            ChunkLoadQueue queue = new(1, -1, 1, -2, 2);
            ConcurrentDictionary<int3, ManagedChunk> chunks = new();

            ManagedChunk chunk = CreateChunk(int3.zero);
            chunk.RefCount = 1;
            chunk.GracePeriodExpiry = double.MaxValue;
            chunks[int3.zero] = chunk;

            // Player is now far away — chunk is no longer in range
            Span<int3> players = stackalloc int3[1];
            players[0] = new int3(100, 0, 0);

            queue.AdjustRefCounts(players, 10.0, 5.0, chunks);

            Assert.AreEqual(0, chunk.RefCount);
            Assert.AreEqual(15.0, chunk.GracePeriodExpiry, 0.001, "Grace period should be currentTime + gracePeriodSeconds");
        }

        /// <summary>SetRenderDistance clamps to minimum 1.</summary>
        [Test]
        public void SetRenderDistance_Clamps()
        {
            ChunkLoadQueue queue = new(5, -1, 3, -2, 4);

            queue.SetRenderDistance(0);
            Assert.AreEqual(1, queue.RenderDistance);

            queue.SetRenderDistance(16);
            Assert.AreEqual(16, queue.RenderDistance);
        }

        /// <summary>Creates a ManagedChunk at the given coordinate, tracking it for cleanup.</summary>
        private ManagedChunk CreateChunk(int3 coord)
        {
            NativeArray<StateId> data = _pool.Checkout();
            ManagedChunk chunk = new(coord, data);
            _createdChunks.Add(chunk);
            return chunk;
        }
    }
}
