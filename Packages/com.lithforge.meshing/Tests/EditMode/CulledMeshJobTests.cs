using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace Lithforge.Meshing.Tests
{
    [TestFixture]
    public sealed class CulledMeshJobTests
    {
        private NativeArray<BlockStateCompact> _stateTable;

        [SetUp]
        public void SetUp()
        {
            // StateId(0) = AIR, StateId(1) = stone (opaque full cube)
            _stateTable = new NativeArray<BlockStateCompact>(2, Allocator.TempJob);
            _stateTable[0] = new BlockStateCompact
            {
                Flags = BlockStateCompact.FlagAir,
                MapColor = 0x00000000,
            };
            _stateTable[1] = new BlockStateCompact
            {
                Flags = BlockStateCompact.FlagOpaque | BlockStateCompact.FlagFullCube,
                MapColor = 0x7F7F7FFF,
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (_stateTable.IsCreated)
            {
                _stateTable.Dispose();
            }
        }

        [Test]
        public void SingleStoneBlock_SixFaces()
        {
            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeList<MeshVertex> vertices = new(256, Allocator.TempJob);
            NativeList<int> indices = new(256, Allocator.TempJob);

            try
            {
                // Place single stone block at center
                int centerIndex = ChunkData.GetIndex(16, 16, 16);
                chunkData[centerIndex] = new StateId(1);

                CulledMeshJob job = new()
                {
                    ChunkData = chunkData,
                    StateTable = _stateTable,
                    Vertices = vertices,
                    Indices = indices,
                };

                job.Schedule().Complete();

                Assert.AreEqual(24, vertices.Length, "Single block should have 24 vertices (6 faces x 4)");
                Assert.AreEqual(36, indices.Length, "Single block should have 36 indices (6 faces x 6)");
            }
            finally
            {
                chunkData.Dispose();
                vertices.Dispose();
                indices.Dispose();
            }
        }

        [Test]
        public void FullStoneChunk_OnlyBorderFaces()
        {
            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeList<MeshVertex> vertices = new(65536, Allocator.TempJob);
            NativeList<int> indices = new(65536, Allocator.TempJob);

            try
            {
                StateId stone = new(1);

                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    chunkData[i] = stone;
                }

                CulledMeshJob job = new()
                {
                    ChunkData = chunkData,
                    StateTable = _stateTable,
                    Vertices = vertices,
                    Indices = indices,
                };

                job.Schedule().Complete();

                // All interior faces are culled. Only 6 outer faces of 32x32 faces each.
                // Each face of the chunk: 32*32 = 1024 quads, 6 faces = 6144 quads
                int expectedFaces = 6 * ChunkConstants.Size * ChunkConstants.Size;
                Assert.AreEqual(expectedFaces * 4, vertices.Length,
                    "Full chunk border faces: " + expectedFaces + " quads");
                Assert.AreEqual(expectedFaces * 6, indices.Length);
            }
            finally
            {
                chunkData.Dispose();
                vertices.Dispose();
                indices.Dispose();
            }
        }

        [Test]
        public void AirChunk_ZeroVertices()
        {
            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeList<MeshVertex> vertices = new(64, Allocator.TempJob);
            NativeList<int> indices = new(64, Allocator.TempJob);

            try
            {
                CulledMeshJob job = new()
                {
                    ChunkData = chunkData,
                    StateTable = _stateTable,
                    Vertices = vertices,
                    Indices = indices,
                };

                job.Schedule().Complete();

                Assert.AreEqual(0, vertices.Length, "Air chunk should have 0 vertices");
                Assert.AreEqual(0, indices.Length, "Air chunk should have 0 indices");
            }
            finally
            {
                chunkData.Dispose();
                vertices.Dispose();
                indices.Dispose();
            }
        }

        [Test]
        public void TwoAdjacentStoneBlocks_SharedFaceCulled()
        {
            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeList<MeshVertex> vertices = new(256, Allocator.TempJob);
            NativeList<int> indices = new(256, Allocator.TempJob);

            try
            {
                // Two adjacent blocks along X axis
                chunkData[ChunkData.GetIndex(16, 16, 16)] = new StateId(1);
                chunkData[ChunkData.GetIndex(17, 16, 16)] = new StateId(1);

                CulledMeshJob job = new()
                {
                    ChunkData = chunkData,
                    StateTable = _stateTable,
                    Vertices = vertices,
                    Indices = indices,
                };

                job.Schedule().Complete();

                // 2 blocks * 6 faces - 2 shared faces = 10 faces
                Assert.AreEqual(40, vertices.Length, "Two adjacent blocks should have 10 faces (40 verts)");
                Assert.AreEqual(60, indices.Length, "Two adjacent blocks should have 10 faces (60 indices)");
            }
            finally
            {
                chunkData.Dispose();
                vertices.Dispose();
                indices.Dispose();
            }
        }
    }
}
