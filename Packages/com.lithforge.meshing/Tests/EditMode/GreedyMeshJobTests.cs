using Lithforge.Meshing.Atlas;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.Meshing.Tests
{
    [TestFixture]
    public sealed class GreedyMeshJobTests
    {
        private NativeArray<BlockStateCompact> _stateTable;
        private NativeArray<AtlasEntry> _atlasEntries;

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
                TexNorth = 1,
                TexSouth = 1,
                TexEast = 1,
                TexWest = 1,
                TexUp = 1,
                TexDown = 1,
            };

            _atlasEntries = new NativeArray<AtlasEntry>(2, Allocator.TempJob);
            _atlasEntries[0] = new AtlasEntry
            {
                OvlPosX = 0xFFFF, OvlNegX = 0xFFFF,
                OvlPosY = 0xFFFF, OvlNegY = 0xFFFF,
                OvlPosZ = 0xFFFF, OvlNegZ = 0xFFFF,
            };
            _atlasEntries[1] = new AtlasEntry
            {
                TexPosX = 1,
                TexNegX = 1,
                TexPosY = 1,
                TexNegY = 1,
                TexPosZ = 1,
                TexNegZ = 1,
                OvlPosX = 0xFFFF, OvlNegX = 0xFFFF,
                OvlPosY = 0xFFFF, OvlNegY = 0xFFFF,
                OvlPosZ = 0xFFFF, OvlNegZ = 0xFFFF,
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (_stateTable.IsCreated) { _stateTable.Dispose(); }
            if (_atlasEntries.IsCreated) { _atlasEntries.Dispose(); }
        }

        private GreedyMeshData CreateMeshData()
        {
            return new GreedyMeshData(Allocator.TempJob);
        }

        private NativeArray<byte> CreateEmptyLightData()
        {
            return new NativeArray<byte>(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
        }

        private JobHandle ScheduleJob(
            NativeArray<StateId> chunkData,
            GreedyMeshData meshData,
            NativeArray<byte> lightData)
        {
            GreedyMeshJob job = new()
            {
                ChunkData = chunkData,
                NeighborPosX = meshData.NeighborPosX,
                NeighborNegX = meshData.NeighborNegX,
                NeighborPosY = meshData.NeighborPosY,
                NeighborNegY = meshData.NeighborNegY,
                NeighborPosZ = meshData.NeighborPosZ,
                NeighborNegZ = meshData.NeighborNegZ,
                StateTable = _stateTable,
                AtlasEntries = _atlasEntries,
                LightData = lightData,
                ChunkCoord = int3.zero,
                OpaqueVertices = meshData.OpaqueVertices,
                OpaqueIndices = meshData.OpaqueIndices,
                CutoutVertices = meshData.CutoutVertices,
                CutoutIndices = meshData.CutoutIndices,
                TranslucentVertices = meshData.TranslucentVertices,
                TranslucentIndices = meshData.TranslucentIndices,
            };

            return job.Schedule();
        }

        /// <summary>Extracts the 3-bit normal index from a packed vertex.</summary>
        private static int GetNormalIndex(PackedMeshVertex v)
        {
            return (int)((v.Word0 >> 18) & 0x7u);
        }

        /// <summary>Extracts the 10-bit base texture index from a packed vertex.</summary>
        private static int GetTexIndex(PackedMeshVertex v)
        {
            return (int)(v.Word1 & 0x3FFu);
        }

        /// <summary>Extracts the 2-bit baseTintType from a packed vertex.</summary>
        private static uint GetBaseTintType(PackedMeshVertex v)
        {
            return (v.Word1 >> 10) & 0x3u;
        }

        /// <summary>Extracts overlay fields from a packed vertex.</summary>
        private static void GetOverlayFields(PackedMeshVertex v,
            out uint hasOverlay, out uint overlayTexIndex, out uint overlayTintType)
        {
            hasOverlay = (v.Word1 >> 12) & 0x1u;
            overlayTexIndex = (v.Word1 >> 13) & 0x3FFu;
            overlayTintType = (v.Word1 >> 23) & 0x3u;
        }

        [Test]
        public void AirChunk_ZeroVertices()
        {
            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> lightData = CreateEmptyLightData();
            GreedyMeshData meshData = CreateMeshData();

            try
            {
                ScheduleJob(chunkData, meshData, lightData).Complete();

                Assert.AreEqual(0, meshData.OpaqueVertices.Length, "Air chunk should have 0 vertices");
                Assert.AreEqual(0, meshData.OpaqueIndices.Length, "Air chunk should have 0 indices");
            }
            finally
            {
                chunkData.Dispose();
                lightData.Dispose();
                meshData.Dispose();
            }
        }

        [Test]
        public void SingleBlock_SixFaces()
        {
            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> lightData = CreateEmptyLightData();
            GreedyMeshData meshData = CreateMeshData();

            try
            {
                // Place single stone block at center
                chunkData[ChunkData.GetIndex(16, 16, 16)] = new StateId(1);

                ScheduleJob(chunkData, meshData, lightData).Complete();

                // Single block = 6 faces = 24 vertices, 36 indices
                Assert.AreEqual(24, meshData.OpaqueVertices.Length,
                    "Single block should have 24 vertices (6 faces x 4)");
                Assert.AreEqual(36, meshData.OpaqueIndices.Length,
                    "Single block should have 36 indices (6 faces x 6)");
            }
            finally
            {
                chunkData.Dispose();
                lightData.Dispose();
                meshData.Dispose();
            }
        }

        [Test]
        public void TwoAdjacentBlocks_SharedFaceCulled()
        {
            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> lightData = CreateEmptyLightData();
            GreedyMeshData meshData = CreateMeshData();

            try
            {
                chunkData[ChunkData.GetIndex(16, 16, 16)] = new StateId(1);
                chunkData[ChunkData.GetIndex(17, 16, 16)] = new StateId(1);

                ScheduleJob(chunkData, meshData, lightData).Complete();

                // Greedy will merge co-planar faces. The shared +X/-X face is culled.
                // Remaining: 2 top, 2 bottom, 2 front, 2 back + 1 left + 1 right
                // But greedy merges adjacent faces on the same plane:
                //   Top: 1 merged quad (2x1), Bottom: 1 merged (2x1)
                //   Front: 1 merged (2x1), Back: 1 merged (2x1)
                //   Left: 1 quad (1x1), Right: 1 quad (1x1)
                // = 6 quads total, but each has 4 verts, 6 indices
                // Actually with AO gating, the merging depends on AO values.
                // Just verify face culling happened: less than 48 verts (12 faces).
                Assert.Less(meshData.OpaqueVertices.Length, 48,
                    "Shared face should be culled, fewer than 12 faces");
                Assert.Greater(meshData.OpaqueVertices.Length, 0,
                    "Should have some visible faces");
            }
            finally
            {
                chunkData.Dispose();
                lightData.Dispose();
                meshData.Dispose();
            }
        }

        [Test]
        public void FullChunk_GreedyMergesReduceQuads()
        {
            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> lightData = CreateEmptyLightData();
            GreedyMeshData meshData = CreateMeshData();

            try
            {
                StateId stone = new(1);

                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    chunkData[i] = stone;
                }

                ScheduleJob(chunkData, meshData, lightData).Complete();

                // Full chunk: only 6 outer faces, each 32x32.
                // Without greedy: 6 * 32 * 32 = 6144 quads = 24576 verts.
                // With greedy (uniform block, uniform AO): each face merges to 1 quad
                // = 6 quads = 24 verts. But AO varies at edges so it won't be perfect.
                // Just verify significant reduction.
                int nonGreedyVertCount = 6 * ChunkConstants.Size * ChunkConstants.Size * 4;
                Assert.Less(meshData.OpaqueVertices.Length, nonGreedyVertCount,
                    "Greedy merging should reduce vertex count vs naive face-per-cell");
                Assert.Greater(meshData.OpaqueVertices.Length, 0,
                    "Full chunk should have some visible faces");
            }
            finally
            {
                chunkData.Dispose();
                lightData.Dispose();
                meshData.Dispose();
            }
        }

        [Test]
        public void VertexColor_ContainsTextureIndex()
        {
            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> lightData = CreateEmptyLightData();
            GreedyMeshData meshData = CreateMeshData();

            try
            {
                chunkData[ChunkData.GetIndex(16, 16, 16)] = new StateId(1);

                ScheduleJob(chunkData, meshData, lightData).Complete();

                // Check that packed vertex contains texture index 1 (stone)
                for (int i = 0; i < meshData.OpaqueVertices.Length; i++)
                {
                    PackedMeshVertex vert = meshData.OpaqueVertices[i];
                    int texIndex = GetTexIndex(vert);
                    Assert.AreEqual(1, texIndex,
                        $"Vertex {i} texIndex should be 1");
                }
            }
            finally
            {
                chunkData.Dispose();
                lightData.Dispose();
                meshData.Dispose();
            }
        }

        [Test]
        public void TintOverlay_BaseTintType_EncodedCorrectly()
        {
            // Configure atlas entry with grass tint (type 1) on PosY face (direction 2)
            // BaseTintPacked: 6 faces x 2 bits, PosY is bits 4-5
            _atlasEntries[1] = new AtlasEntry
            {
                TexPosX = 1, TexNegX = 1, TexPosY = 1, TexNegY = 1, TexPosZ = 1, TexNegZ = 1,
                OvlPosX = 0xFFFF, OvlNegX = 0xFFFF, OvlPosY = 0xFFFF,
                OvlNegY = 0xFFFF, OvlPosZ = 0xFFFF, OvlNegZ = 0xFFFF,
                BaseTintPacked = (ushort)(1 << (2 * 2)),
                OverlayTintPacked = 0,
            };

            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> lightData = CreateEmptyLightData();
            GreedyMeshData meshData = CreateMeshData();

            try
            {
                chunkData[ChunkData.GetIndex(16, 16, 16)] = new StateId(1);

                ScheduleJob(chunkData, meshData, lightData).Complete();

                // Find PosY face vertices (normalIndex == 2)
                int posYCount = 0;

                for (int i = 0; i < meshData.OpaqueVertices.Length; i++)
                {
                    PackedMeshVertex vert = meshData.OpaqueVertices[i];

                    if (GetNormalIndex(vert) == 2) // +Y
                    {
                        uint baseTint = GetBaseTintType(vert);
                        Assert.AreEqual(1u, baseTint,
                            $"PosY vertex {i}: baseTintType should be 1 (grass)");
                        posYCount++;
                    }
                }

                Assert.Greater(posYCount, 0, "Should have found PosY face vertices");
            }
            finally
            {
                chunkData.Dispose();
                lightData.Dispose();
                meshData.Dispose();
            }
        }

        [Test]
        public void TintOverlay_Overlay_EncodedCorrectly()
        {
            // Configure atlas entry with overlay on PosX face (direction 0)
            // overlayTexIdx=42, overlayTintType=2 (foliage) on PosX
            ushort overlayTexIdx = 42;
            _atlasEntries[1] = new AtlasEntry
            {
                TexPosX = 1, TexNegX = 1, TexPosY = 1, TexNegY = 1, TexPosZ = 1, TexNegZ = 1,
                OvlPosX = overlayTexIdx, OvlNegX = 0xFFFF, OvlPosY = 0xFFFF,
                OvlNegY = 0xFFFF, OvlPosZ = 0xFFFF, OvlNegZ = 0xFFFF,
                BaseTintPacked = 0,
                OverlayTintPacked = (ushort)(2 << (0 * 2)),
            };

            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> lightData = CreateEmptyLightData();
            GreedyMeshData meshData = CreateMeshData();

            try
            {
                chunkData[ChunkData.GetIndex(16, 16, 16)] = new StateId(1);

                ScheduleJob(chunkData, meshData, lightData).Complete();

                // Find PosX face vertices (normalIndex == 0)
                int posXCount = 0;

                for (int i = 0; i < meshData.OpaqueVertices.Length; i++)
                {
                    PackedMeshVertex vert = meshData.OpaqueVertices[i];

                    if (GetNormalIndex(vert) == 0) // +X
                    {
                        uint baseTint = GetBaseTintType(vert);
                        GetOverlayFields(vert,
                            out uint hasOverlayVal, out uint ovlTexIdx, out uint overlayTint);

                        Assert.AreEqual(0u, baseTint,
                            $"PosX vertex {i}: no base tint expected");
                        Assert.AreEqual(2u, overlayTint,
                            $"PosX vertex {i}: overlayTintType should be 2 (foliage)");
                        Assert.AreEqual(1u, hasOverlayVal,
                            $"PosX vertex {i}: hasOverlay should be 1");
                        Assert.AreEqual(42u, ovlTexIdx,
                            $"PosX vertex {i}: overlayTexIndex should be 42");
                        posXCount++;
                    }
                }

                Assert.Greater(posXCount, 0, "Should have found PosX face vertices");
            }
            finally
            {
                chunkData.Dispose();
                lightData.Dispose();
                meshData.Dispose();
            }
        }
    }
}
