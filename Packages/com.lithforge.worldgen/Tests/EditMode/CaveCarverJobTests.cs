using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Noise;
using Lithforge.WorldGen.Stages;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Tests
{
    [TestFixture]
    public sealed class CaveCarverJobTests
    {
        private NativeNoiseConfig _caveNoise;
        private StateId _stoneId;
        private StateId _airId;
        private StateId _waterId;

        [SetUp]
        public void SetUp()
        {
            _stoneId = new StateId(1);
            _airId = StateId.Air;
            _waterId = new StateId(2);

            _caveNoise = new NativeNoiseConfig
            {
                Frequency = 0.03f,
                Lacunarity = 2.0f,
                Persistence = 0.5f,
                HeightScale = 1.0f,
                Octaves = 2,
                SeedOffset = 0,
            };
        }

        [Test]
        public void Execute_CarvesSomeBlocks_InStoneChunk()
        {
            NativeArray<StateId> chunkData = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob);

            try
            {
                // Fill entire chunk with stone
                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    chunkData[i] = _stoneId;
                }

                CaveCarverJob job = new CaveCarverJob
                {
                    ChunkData = chunkData,
                    Seed = 42L,
                    ChunkCoord = new int3(0, 1, 0),
                    CaveNoise = _caveNoise,
                    AirId = _airId,
                    WaterId = _waterId,
                    SeaLevel = 64,
                };

                job.Schedule().Complete();

                // Count air blocks created by cave carving
                int airCount = 0;

                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    if (chunkData[i].Equals(_airId))
                    {
                        airCount++;
                    }
                }

                // Caves should carve some blocks but not too many
                Assert.Greater(airCount, 0, "Caves should carve at least some blocks");
                Assert.Less(airCount, ChunkConstants.Volume / 2, "Caves should not carve more than half the chunk");
            }
            finally
            {
                chunkData.Dispose();
            }
        }

        [Test]
        public void Execute_DoesNotCarveWater()
        {
            NativeArray<StateId> chunkData = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob);

            try
            {
                // Fill entire chunk with water
                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    chunkData[i] = _waterId;
                }

                CaveCarverJob job = new CaveCarverJob
                {
                    ChunkData = chunkData,
                    Seed = 42L,
                    ChunkCoord = new int3(0, 1, 0),
                    CaveNoise = _caveNoise,
                    AirId = _airId,
                    WaterId = _waterId,
                    SeaLevel = 64,
                };

                job.Schedule().Complete();

                // No water blocks should be carved to air
                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    Assert.IsFalse(chunkData[i].Equals(_airId),
                        "Water blocks should never be carved to air");
                }
            }
            finally
            {
                chunkData.Dispose();
            }
        }

        [Test]
        public void Execute_DoesNotCarveNearSeaLevel()
        {
            NativeArray<StateId> chunkData = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob);

            try
            {
                // Fill entire chunk with stone
                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    chunkData[i] = _stoneId;
                }

                int seaLevel = 64;

                // Chunk at y=2 means world y range 64-95 (spans sea level)
                CaveCarverJob job = new CaveCarverJob
                {
                    ChunkData = chunkData,
                    Seed = 42L,
                    ChunkCoord = new int3(0, 2, 0),
                    CaveNoise = _caveNoise,
                    AirId = _airId,
                    WaterId = _waterId,
                    SeaLevel = seaLevel,
                };

                job.Schedule().Complete();

                // Check that blocks near sea level (within 4 blocks) are not carved
                int chunkWorldY = 2 * ChunkConstants.Size;

                for (int z = 0; z < ChunkConstants.Size; z++)
                {
                    for (int x = 0; x < ChunkConstants.Size; x++)
                    {
                        for (int y = 0; y < ChunkConstants.Size; y++)
                        {
                            int worldY = chunkWorldY + y;

                            if (worldY >= seaLevel - 4 && worldY <= seaLevel)
                            {
                                int index = ChunkData.GetIndex(x, y, z);
                                Assert.IsFalse(chunkData[index].Equals(_airId),
                                    $"Block near sea level at worldY={worldY} should not be carved");
                            }
                        }
                    }
                }
            }
            finally
            {
                chunkData.Dispose();
            }
        }

        [Test]
        public void Execute_DoesNotCarveBelowMinY()
        {
            NativeArray<StateId> chunkData = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob);

            try
            {
                // Fill entire chunk with stone
                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    chunkData[i] = _stoneId;
                }

                // Chunk at y=0 means world y range 0-31
                CaveCarverJob job = new CaveCarverJob
                {
                    ChunkData = chunkData,
                    Seed = 42L,
                    ChunkCoord = new int3(0, 0, 0),
                    CaveNoise = _caveNoise,
                    AirId = _airId,
                    WaterId = _waterId,
                    SeaLevel = 64,
                };

                job.Schedule().Complete();

                // Blocks at worldY < 5 should never be carved
                for (int z = 0; z < ChunkConstants.Size; z++)
                {
                    for (int x = 0; x < ChunkConstants.Size; x++)
                    {
                        for (int y = 0; y < 5; y++)
                        {
                            int index = ChunkData.GetIndex(x, y, z);
                            Assert.AreEqual(_stoneId, chunkData[index],
                                $"Block below minCarveY at y={y} should not be carved");
                        }
                    }
                }
            }
            finally
            {
                chunkData.Dispose();
            }
        }

        [Test]
        public void Execute_Deterministic_SameSeedSameResult()
        {
            NativeArray<StateId> chunkData1 = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<StateId> chunkData2 = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob);

            try
            {
                // Fill both with stone
                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    chunkData1[i] = _stoneId;
                    chunkData2[i] = _stoneId;
                }

                CaveCarverJob job1 = new CaveCarverJob
                {
                    ChunkData = chunkData1,
                    Seed = 42L,
                    ChunkCoord = new int3(1, 1, 1),
                    CaveNoise = _caveNoise,
                    AirId = _airId,
                    WaterId = _waterId,
                    SeaLevel = 64,
                };

                CaveCarverJob job2 = new CaveCarverJob
                {
                    ChunkData = chunkData2,
                    Seed = 42L,
                    ChunkCoord = new int3(1, 1, 1),
                    CaveNoise = _caveNoise,
                    AirId = _airId,
                    WaterId = _waterId,
                    SeaLevel = 64,
                };

                job1.Schedule().Complete();
                job2.Schedule().Complete();

                // Results should be identical
                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    Assert.AreEqual(chunkData1[i], chunkData2[i],
                        $"Caves should be deterministic, mismatch at index {i}");
                }
            }
            finally
            {
                chunkData1.Dispose();
                chunkData2.Dispose();
            }
        }
    }
}
