using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Lighting;
using Lithforge.WorldGen.Stages;

using NUnit.Framework;

using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Tests
{
    [TestFixture]
    public sealed class LightRemovalJobTests
    {
        [SetUp]
        public void SetUp()
        {
            _chunkData = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob);
            _lightData = new NativeArray<byte>(
                ChunkConstants.Volume, Allocator.TempJob);
            _heightMap = new NativeArray<int>(
                ChunkConstants.SizeSquared, Allocator.TempJob);

            // State 0 = air (transparent), State 1 = stone (opaque)
            _stateTable = new NativeArray<BlockStateCompact>(2, Allocator.TempJob);
            _stateTable[0] = new BlockStateCompact
            {
                Flags = 0,
            }; // air: transparent
            _stateTable[1] = new BlockStateCompact
            {
                Flags = BlockStateCompact.FlagOpaque,
            }; // stone: opaque
        }

        [TearDown]
        public void TearDown()
        {
            if (_chunkData.IsCreated)
            {
                _chunkData.Dispose();
            }

            if (_stateTable.IsCreated)
            {
                _stateTable.Dispose();
            }

            if (_lightData.IsCreated)
            {
                _lightData.Dispose();
            }

            if (_heightMap.IsCreated)
            {
                _heightMap.Dispose();
            }
        }
        private NativeArray<StateId> _chunkData;
        private NativeArray<BlockStateCompact> _stateTable;
        private NativeArray<byte> _lightData;
        private NativeArray<int> _heightMap;

        [Test]
        public void PlaceOpaqueBlock_ZeroesSunlightAtPosition()
        {
            // Fill entire chunk with sun=15 air
            for (int i = 0; i < ChunkConstants.Volume; i++)
            {
                _lightData[i] = LightUtils.Pack(15, 0);
            }

            int testX = 16;
            int testY = 16;
            int testZ = 16;
            int blockIndex = ChunkData.GetIndex(testX, testY, testZ);

            // Place opaque block
            StateId stoneId = new(1);
            _chunkData[blockIndex] = stoneId;

            // Set heightmap: surface is above, so this is a below-surface edit
            int colIdx = testZ * ChunkConstants.Size + testX;
            _heightMap[colIdx] = testY + 5;

            NativeArray<int> changedIndices = new(1, Allocator.TempJob);
            changedIndices[0] = blockIndex;

            NativeArray<NativeBorderLightEntry> borderSeeds = new(0, Allocator.TempJob);
            NativeList<NativeBorderLightEntry> borderOutput = new(16, Allocator.TempJob);

            LightRemovalJob job = new()
            {
                LightData = _lightData,
                ChunkData = _chunkData,
                StateTable = _stateTable,
                HeightMap = _heightMap,
                ChunkWorldY = 0,
                ChangedIndices = changedIndices,
                BorderRemovalSeeds = borderSeeds,
                BorderLightOutput = borderOutput,
            };

            job.Schedule().Complete();

            // Sunlight at the placed block should be 0
            byte sun = LightUtils.GetSunLight(_lightData[blockIndex]);
            Assert.AreEqual(0, sun, "Sunlight at placed opaque block should be 0");

            changedIndices.Dispose();
            borderSeeds.Dispose();
            borderOutput.Dispose();
        }

        [Test]
        public void PlaceOpaqueBlock_InSunlightColumn_ZeroesColumnBelow()
        {
            // Fill chunk with sun=15 air (simulating full sunlight column)
            for (int i = 0; i < ChunkConstants.Volume; i++)
            {
                _lightData[i] = LightUtils.Pack(15, 0);
            }

            int testX = 5;
            int testZ = 5;
            int blockY = 20;
            int blockIndex = ChunkData.GetIndex(testX, blockY, testZ);

            // Place opaque block
            StateId stoneId = new(1);
            _chunkData[blockIndex] = stoneId;

            // Set heightmap at or below the placed block so column-write is triggered
            int colIdx = testZ * ChunkConstants.Size + testX;
            _heightMap[colIdx] = blockY - 1;

            NativeArray<int> changedIndices = new(1, Allocator.TempJob);
            changedIndices[0] = blockIndex;

            NativeArray<NativeBorderLightEntry> borderSeeds = new(0, Allocator.TempJob);
            NativeList<NativeBorderLightEntry> borderOutput = new(16, Allocator.TempJob);

            LightRemovalJob job = new()
            {
                LightData = _lightData,
                ChunkData = _chunkData,
                StateTable = _stateTable,
                HeightMap = _heightMap,
                ChunkWorldY = 0,
                ChangedIndices = changedIndices,
                BorderRemovalSeeds = borderSeeds,
                BorderLightOutput = borderOutput,
            };

            job.Schedule().Complete();

            // Column below the placed block should be zeroed
            for (int y = 0; y < blockY; y++)
            {
                int index = ChunkData.GetIndex(testX, y, testZ);
                byte sun = LightUtils.GetSunLight(_lightData[index]);
                Assert.AreEqual(0, sun, $"Sunlight at y={y} below opaque block should be 0");
            }

            // Adjacent column should be unaffected (still 15)
            for (int y = 0; y < ChunkConstants.Size; y++)
            {
                int index = ChunkData.GetIndex(testX + 1, y, testZ);
                byte sun = LightUtils.GetSunLight(_lightData[index]);
                Assert.AreEqual(15, sun, $"Sunlight in adjacent column at y={y} should be 15");
            }

            changedIndices.Dispose();
            borderSeeds.Dispose();
            borderOutput.Dispose();
        }

        [Test]
        public void BreakOpaqueBlock_RestoresSunlightColumn()
        {
            int testX = 10;
            int testZ = 10;
            int stoneY = 20;

            // Set up: stone at y=20, air everywhere else
            // Sun=15 above the stone, sun=0 below
            StateId stoneId = new(1);
            _chunkData[ChunkData.GetIndex(testX, stoneY, testZ)] = stoneId;

            for (int y = stoneY + 1; y < ChunkConstants.Size; y++)
            {
                int index = ChunkData.GetIndex(testX, y, testZ);
                _lightData[index] = LightUtils.Pack(15, 0);
            }

            // Also fill adjacent columns with sun=15 so reseed works
            for (int y = 0; y < ChunkConstants.Size; y++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0)
                        {
                            continue;
                        }

                        int nx = testX + dx;
                        int nz = testZ + dz;

                        if (nx >= 0 && nx < ChunkConstants.Size &&
                            nz >= 0 && nz < ChunkConstants.Size)
                        {
                            int index = ChunkData.GetIndex(nx, y, nz);
                            _lightData[index] = LightUtils.Pack(15, 0);
                        }
                    }
                }
            }

            // Heightmap: surface at the stone block
            int colIdx = testZ * ChunkConstants.Size + testX;
            _heightMap[colIdx] = stoneY;

            // Now break the stone (set to air via ChangedIndices)
            _chunkData[ChunkData.GetIndex(testX, stoneY, testZ)] = new StateId(0);

            NativeArray<int> changedIndices = new(1, Allocator.TempJob);
            changedIndices[0] = ChunkData.GetIndex(testX, stoneY, testZ);

            NativeArray<NativeBorderLightEntry> borderSeeds = new(0, Allocator.TempJob);
            NativeList<NativeBorderLightEntry> borderOutput = new(16, Allocator.TempJob);

            LightRemovalJob job = new()
            {
                LightData = _lightData,
                ChunkData = _chunkData,
                StateTable = _stateTable,
                HeightMap = _heightMap,
                ChunkWorldY = 0,
                ChangedIndices = changedIndices,
                BorderRemovalSeeds = borderSeeds,
                BorderLightOutput = borderOutput,
            };

            job.Schedule().Complete();

            // Sun=15 should be restored at the broken block position
            byte sunAtBroken = LightUtils.GetSunLight(
                _lightData[ChunkData.GetIndex(testX, stoneY, testZ)]);
            Assert.AreEqual(15, sunAtBroken, "Sunlight at broken block should be restored to 15");

            // Sun=15 should be restored below the broken block
            for (int y = 0; y < stoneY; y++)
            {
                int index = ChunkData.GetIndex(testX, y, testZ);
                byte sun = LightUtils.GetSunLight(_lightData[index]);
                Assert.AreEqual(15, sun, $"Sunlight at y={y} below broken block should be restored to 15");
            }

            changedIndices.Dispose();
            borderSeeds.Dispose();
            borderOutput.Dispose();
        }

        [Test]
        public void BorderRemovalSeed_ZeroesLightAtPosition()
        {
            // Fill chunk with sun=15 air
            for (int i = 0; i < ChunkConstants.Volume; i++)
            {
                _lightData[i] = LightUtils.Pack(15, 0);
            }

            // Set heightmap high so column-write path is triggered for sun=15 border seeds
            for (int i = 0; i < ChunkConstants.SizeSquared; i++)
            {
                _heightMap[i] = -1;
            }

            // Create border removal seed at x=0 face
            NativeArray<int> changedIndices = new(0, Allocator.TempJob);

            NativeBorderLightEntry seed = new()
            {
                LocalPosition = new int3(0, 5, 5), PackedLight = LightUtils.Pack(15, 0), Face = 1, // -X face
            };

            NativeArray<NativeBorderLightEntry> borderSeeds = new(1, Allocator.TempJob);
            borderSeeds[0] = seed;

            NativeList<NativeBorderLightEntry> borderOutput = new(16, Allocator.TempJob);

            LightRemovalJob job = new()
            {
                LightData = _lightData,
                ChunkData = _chunkData,
                StateTable = _stateTable,
                HeightMap = _heightMap,
                ChunkWorldY = 0,
                ChangedIndices = changedIndices,
                BorderRemovalSeeds = borderSeeds,
                BorderLightOutput = borderOutput,
            };

            job.Schedule().Complete();

            // Sun should be zeroed at the seed position
            int seedIndex = ChunkData.GetIndex(0, 5, 5);
            byte sunAtSeed = LightUtils.GetSunLight(_lightData[seedIndex]);
            Assert.AreEqual(0, sunAtSeed, "Sunlight at border removal seed should be 0");

            changedIndices.Dispose();
            borderSeeds.Dispose();
            borderOutput.Dispose();
        }

        [Test]
        public void PlaceBlock_BfsVolumeIsBounded()
        {
            // Fill chunk with sun=15 air
            for (int i = 0; i < ChunkConstants.Volume; i++)
            {
                _lightData[i] = LightUtils.Pack(15, 0);
            }

            int testX = 16;
            int testY = 16;
            int testZ = 16;
            int blockIndex = ChunkData.GetIndex(testX, testY, testZ);

            // Place one opaque block at center
            StateId stoneId = new(1);
            _chunkData[blockIndex] = stoneId;

            // Set heightmap: surface is above, so this is a below-surface edit
            int colIdx = testZ * ChunkConstants.Size + testX;
            _heightMap[colIdx] = testY + 5;

            NativeArray<int> changedIndices = new(1, Allocator.TempJob);
            changedIndices[0] = blockIndex;

            NativeArray<NativeBorderLightEntry> borderSeeds = new(0, Allocator.TempJob);
            NativeList<NativeBorderLightEntry> borderOutput = new(16, Allocator.TempJob);

            LightRemovalJob job = new()
            {
                LightData = _lightData,
                ChunkData = _chunkData,
                StateTable = _stateTable,
                HeightMap = _heightMap,
                ChunkWorldY = 0,
                ChangedIndices = changedIndices,
                BorderRemovalSeeds = borderSeeds,
                BorderLightOutput = borderOutput,
            };

            job.Schedule().Complete();

            // Count changed voxels (voxels with sun != 15 that aren't the placed block)
            int changedCount = 0;

            for (int i = 0; i < ChunkConstants.Volume; i++)
            {
                if (i == blockIndex)
                {
                    continue;
                }

                byte sun = LightUtils.GetSunLight(_lightData[i]);

                if (sun != 15)
                {
                    changedCount++;
                }
            }

            // The blast radius of a single placed block should be bounded.
            // Only the immediate vicinity (within ~1 block radius) should be affected.
            Assert.Less(changedCount, 500,
                $"BFS volume should be bounded (changed {changedCount} voxels, expected < 500)");

            changedIndices.Dispose();
            borderSeeds.Dispose();
            borderOutput.Dispose();
        }

        [Test]
        public void CollectBorderLightLeaks_ProducesEntriesForLitBorderVoxels()
        {
            // Set sun=10 at position (0, 5, 5) — on the -X border face
            int borderIndex = ChunkData.GetIndex(0, 5, 5);
            _lightData[borderIndex] = LightUtils.Pack(10, 0);

            // Run LightRemovalJob with empty inputs (no-op removal, just border collection)
            NativeArray<int> changedIndices = new(0, Allocator.TempJob);
            NativeArray<NativeBorderLightEntry> borderSeeds = new(0, Allocator.TempJob);
            NativeList<NativeBorderLightEntry> borderOutput = new(16, Allocator.TempJob);

            LightRemovalJob job = new()
            {
                LightData = _lightData,
                ChunkData = _chunkData,
                StateTable = _stateTable,
                HeightMap = _heightMap,
                ChunkWorldY = 0,
                ChangedIndices = changedIndices,
                BorderRemovalSeeds = borderSeeds,
                BorderLightOutput = borderOutput,
            };

            job.Schedule().Complete();

            // Should have at least one border entry for the lit voxel
            Assert.Greater(borderOutput.Length, 0,
                "BorderLightOutput should contain entries for lit border voxels");

            // Find the entry for our specific position
            bool foundEntry = false;

            for (int i = 0; i < borderOutput.Length; i++)
            {
                NativeBorderLightEntry entry = borderOutput[i];

                if (entry.LocalPosition.x == 0 &&
                    entry.LocalPosition.y == 5 &&
                    entry.LocalPosition.z == 5)
                {
                    foundEntry = true;
                    byte entrySun = LightUtils.GetSunLight(entry.PackedLight);
                    Assert.AreEqual(10, entrySun,
                        "Border entry should have sun=10");
                    break;
                }
            }

            Assert.IsTrue(foundEntry, "Should find a border entry at (0, 5, 5)");

            changedIndices.Dispose();
            borderSeeds.Dispose();
            borderOutput.Dispose();
        }
    }
}
