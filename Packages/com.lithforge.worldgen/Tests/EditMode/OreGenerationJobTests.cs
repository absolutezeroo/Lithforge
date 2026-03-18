using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Ore;
using Lithforge.WorldGen.Stages;
using NUnit.Framework;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Tests
{
    [TestFixture]
    public sealed class OreGenerationJobTests
    {
        private StateId _stoneId;
        private StateId _oreId;

        [SetUp]
        public void SetUp()
        {
            _stoneId = new StateId(1);
            _oreId = new StateId(10);
        }

        [Test]
        public void Execute_PlacesOreInStone()
        {
            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<NativeOreConfig> oreConfigs = new(
                1, Allocator.TempJob);

            try
            {
                // Fill chunk with stone
                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    chunkData[i] = _stoneId;
                }

                oreConfigs[0] = new NativeOreConfig
                {
                    OreStateId = _oreId,
                    ReplaceStateId = _stoneId,
                    MinY = 0,
                    MaxY = 128,
                    VeinSize = 17,
                    Frequency = 20.0f,
                    OreType = 1, // blob
                };

                OreGenerationJob job = new()
                {
                    ChunkData = chunkData,
                    OreConfigs = oreConfigs,
                    Seed = 42L,
                    ChunkCoord = new int3(0, 1, 0),
                    StoneId = _stoneId,
                };

                job.Schedule().Complete();

                // Count ore blocks
                int oreCount = 0;

                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    if (chunkData[i].Equals(_oreId))
                    {
                        oreCount++;
                    }
                }

                Assert.Greater(oreCount, 0, "Should have placed at least some ore");
            }
            finally
            {
                chunkData.Dispose();
                oreConfigs.Dispose();
            }
        }

        [Test]
        public void Execute_DoesNotPlaceOreInAir()
        {
            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<NativeOreConfig> oreConfigs = new(
                1, Allocator.TempJob);

            try
            {
                // Chunk is all air (StateId 0)
                oreConfigs[0] = new NativeOreConfig
                {
                    OreStateId = _oreId,
                    ReplaceStateId = _stoneId,
                    MinY = 0,
                    MaxY = 128,
                    VeinSize = 17,
                    Frequency = 20.0f,
                    OreType = 1,
                };

                OreGenerationJob job = new()
                {
                    ChunkData = chunkData,
                    OreConfigs = oreConfigs,
                    Seed = 42L,
                    ChunkCoord = new int3(0, 1, 0),
                    StoneId = _stoneId,
                };

                job.Schedule().Complete();

                // No ore should be placed in air
                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    Assert.IsFalse(chunkData[i].Equals(_oreId),
                        "Ore should not be placed in air");
                }
            }
            finally
            {
                chunkData.Dispose();
                oreConfigs.Dispose();
            }
        }

        [Test]
        public void Execute_RespectsYRange()
        {
            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<NativeOreConfig> oreConfigs = new(
                1, Allocator.TempJob);

            try
            {
                // Fill chunk with stone
                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    chunkData[i] = _stoneId;
                }

                // Ore only below Y=16
                oreConfigs[0] = new NativeOreConfig
                {
                    OreStateId = _oreId,
                    ReplaceStateId = _stoneId,
                    MinY = 0,
                    MaxY = 16,
                    VeinSize = 9,
                    Frequency = 13.0f,
                    OreType = 1,
                };

                // Chunk at y=2 means world y range 64-95, entirely above MaxY=16
                OreGenerationJob job = new()
                {
                    ChunkData = chunkData,
                    OreConfigs = oreConfigs,
                    Seed = 42L,
                    ChunkCoord = new int3(0, 2, 0),
                    StoneId = _stoneId,
                };

                job.Schedule().Complete();

                // No ore should appear since chunk is above the Y range
                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    Assert.AreEqual(_stoneId, chunkData[i],
                        "No ore should be placed above max Y range");
                }
            }
            finally
            {
                chunkData.Dispose();
                oreConfigs.Dispose();
            }
        }

        [Test]
        public void Execute_Deterministic_SameSeedSameResult()
        {
            NativeArray<StateId> chunkData1 = new(
                ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<StateId> chunkData2 = new(
                ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<NativeOreConfig> oreConfigs = new(
                1, Allocator.TempJob);

            try
            {
                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    chunkData1[i] = _stoneId;
                    chunkData2[i] = _stoneId;
                }

                oreConfigs[0] = new NativeOreConfig
                {
                    OreStateId = _oreId,
                    ReplaceStateId = _stoneId,
                    MinY = 0,
                    MaxY = 128,
                    VeinSize = 17,
                    Frequency = 20.0f,
                    OreType = 1,
                };

                OreGenerationJob job1 = new()
                {
                    ChunkData = chunkData1,
                    OreConfigs = oreConfigs,
                    Seed = 42L,
                    ChunkCoord = new int3(3, 1, 7),
                    StoneId = _stoneId,
                };

                OreGenerationJob job2 = new()
                {
                    ChunkData = chunkData2,
                    OreConfigs = oreConfigs,
                    Seed = 42L,
                    ChunkCoord = new int3(3, 1, 7),
                    StoneId = _stoneId,
                };

                job1.Schedule().Complete();
                job2.Schedule().Complete();

                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    Assert.AreEqual(chunkData1[i], chunkData2[i],
                        $"Ore generation should be deterministic, mismatch at index {i}");
                }
            }
            finally
            {
                chunkData1.Dispose();
                chunkData2.Dispose();
                oreConfigs.Dispose();
            }
        }
    }
}
