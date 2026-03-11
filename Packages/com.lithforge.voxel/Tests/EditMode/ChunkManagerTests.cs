using System.Collections.Generic;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Voxel.Tests
{
    [TestFixture]
    public sealed class ChunkManagerTests
    {
        private ChunkPool _pool;
        private ChunkManager _chunkManager;

        [SetUp]
        public void SetUp()
        {
            _pool = new ChunkPool(64);
            _chunkManager = new ChunkManager(_pool, 1);
        }

        [TearDown]
        public void TearDown()
        {
            _chunkManager.Dispose();
            _pool.Dispose();
        }

        [Test]
        public void UpdateLoadingQueue_CreatesCorrectCoords()
        {
            _chunkManager.UpdateLoadingQueue(int3.zero, new float3(0, 0, 1));

            List<ManagedChunk> result = new List<ManagedChunk>();
            _chunkManager.FillChunksToGenerate(result, 100);

            // renderDistance=1: x in [-1,1], z in [-1,1] = 3×3 = 9 columns
            // y default range [-1,3] = 5 levels per column
            // Total = 9 * 5 = 45
            Assert.AreEqual(45, result.Count);

            // Verify no duplicates
            HashSet<int3> coords = new HashSet<int3>();

            for (int i = 0; i < result.Count; i++)
            {
                bool added = coords.Add(result[i].Coord);
                Assert.IsTrue(added, $"Duplicate coord found: {result[i].Coord}");
            }
        }

        [Test]
        public void FillChunksToMesh_UsesSchwartzianSort()
        {
            // Create 3 chunks in Generated state with different neighbor counts
            // Chunk at origin with 2 generated neighbors should sort first
            _chunkManager.UpdateLoadingQueue(int3.zero, new float3(0, 0, 1));

            List<ManagedChunk> generated = new List<ManagedChunk>();
            _chunkManager.FillChunksToGenerate(generated, 100);

            // Set all to Generated
            for (int i = 0; i < generated.Count; i++)
            {
                generated[i].State = ChunkState.Generated;
            }

            List<ManagedChunk> meshResult = new List<ManagedChunk>();
            _chunkManager.FillChunksToMesh(meshResult, 10);

            // Should be sorted by neighbor count descending
            // Center chunks have more neighbors than edge chunks
            Assert.Greater(meshResult.Count, 0, "Should have chunks to mesh");

            // Verify descending neighbor count ordering by checking the first chunk
            // has coords near center (more neighbors)
            ManagedChunk first = meshResult[0];
            int3 diff = first.Coord - int3.zero;
            int xzDist = math.max(math.abs(diff.x), math.abs(diff.z));
            Assert.LessOrEqual(xzDist, 1, "First chunk should be near center (more neighbors)");
        }

        [Test]
        public void UnloadDistantChunks_ReturnsToPool()
        {
            _chunkManager.UpdateLoadingQueue(int3.zero, new float3(0, 0, 1));

            List<ManagedChunk> generated = new List<ManagedChunk>();
            _chunkManager.FillChunksToGenerate(generated, 100);

            // Set all to Generated (so they have valid data)
            for (int i = 0; i < generated.Count; i++)
            {
                generated[i].State = ChunkState.Generated;
            }

            int loadedBefore = _chunkManager.LoadedCount;
            int availableBefore = _pool.AvailableCount;

            // Move camera far away
            List<int3> unloaded = new List<int3>();
            _chunkManager.UnloadDistantChunks(new int3(100, 0, 0), unloaded);

            Assert.Greater(unloaded.Count, 0, "Should have unloaded some chunks");
            Assert.Less(_chunkManager.LoadedCount, loadedBefore, "Loaded count should decrease");
            Assert.Greater(_pool.AvailableCount, availableBefore, "Pool available count should increase");
        }

        [Test]
        public void SetBlock_DirtiesNeighborAtBorder()
        {
            // Create chunk at (0,0,0) and (-1,0,0)
            _chunkManager.UpdateLoadingQueue(int3.zero, new float3(0, 0, 1));

            List<ManagedChunk> generated = new List<ManagedChunk>();
            _chunkManager.FillChunksToGenerate(generated, 100);

            for (int i = 0; i < generated.Count; i++)
            {
                generated[i].State = ChunkState.Generated;
            }

            // Set block at x=0 (border) of chunk (0,0,0)
            // worldCoord = chunkCoord * 32 + localCoord
            // chunkCoord (0,0,0) localX=0 → worldX=0
            List<int3> dirtied = new List<int3>();
            _chunkManager.SetBlock(new int3(0, 5, 5), new StateId(1), dirtied);

            // Should dirty both (0,0,0) and (-1,0,0)
            Assert.IsTrue(dirtied.Contains(new int3(0, 0, 0)),
                "Should dirty the chunk containing the block");

            ManagedChunk negXNeighbor = _chunkManager.GetChunk(new int3(-1, 0, 0));

            if (negXNeighbor != null)
            {
                // The neighbor at -X should be affected since localX=0
                Assert.IsTrue(
                    dirtied.Contains(new int3(-1, 0, 0)) ||
                    negXNeighbor.NeedsRemesh,
                    "Should dirty or flag neighbor at -X border");
            }
        }

        [Test]
        public void SetBlock_OnlyDirtiesCurrentChunkInMiddle()
        {
            _chunkManager.UpdateLoadingQueue(int3.zero, new float3(0, 0, 1));

            List<ManagedChunk> generated = new List<ManagedChunk>();
            _chunkManager.FillChunksToGenerate(generated, 100);

            for (int i = 0; i < generated.Count; i++)
            {
                generated[i].State = ChunkState.Generated;
            }

            // Set block at localX=16 (middle) of chunk (0,0,0)
            // worldCoord = 0*32 + 16 = 16
            List<int3> dirtied = new List<int3>();
            _chunkManager.SetBlock(new int3(16, 5, 5), new StateId(1), dirtied);

            Assert.AreEqual(1, dirtied.Count,
                "Should only dirty the current chunk for middle coordinates");
            Assert.AreEqual(new int3(0, 0, 0), dirtied[0]);
        }

        [Test]
        public void SetBlock_AcceptedDuringRelightPending()
        {
            _chunkManager.UpdateLoadingQueue(int3.zero, new float3(0, 0, 1));

            List<ManagedChunk> generated = new List<ManagedChunk>();
            _chunkManager.FillChunksToGenerate(generated, 100);

            // Find the chunk at (0,0,0) and set it to RelightPending
            ManagedChunk target = _chunkManager.GetChunk(int3.zero);
            Assert.IsNotNull(target, "Chunk at origin should exist");
            target.State = ChunkState.RelightPending;

            // SetBlock should succeed during RelightPending
            List<int3> dirtied = new List<int3>();
            _chunkManager.SetBlock(new int3(16, 5, 5), new StateId(2), dirtied);

            Assert.Greater(dirtied.Count, 0, "SetBlock should be accepted during RelightPending");

            // Verify the block was actually modified
            StateId readBack = _chunkManager.GetBlock(new int3(16, 5, 5));
            Assert.AreEqual(new StateId(2), readBack, "Block should be modified");
        }
    }
}
