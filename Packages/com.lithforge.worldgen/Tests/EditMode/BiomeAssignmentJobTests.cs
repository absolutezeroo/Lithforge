using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Biome;
using Lithforge.WorldGen.Noise;
using Lithforge.WorldGen.Stages;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Tests
{
    [TestFixture]
    public sealed class BiomeAssignmentJobTests
    {
        private NativeNoiseConfig _temperatureNoise;
        private NativeNoiseConfig _humidityNoise;

        [SetUp]
        public void SetUp()
        {
            _temperatureNoise = new NativeNoiseConfig
            {
                Frequency = 0.002f,
                Lacunarity = 2.0f,
                Persistence = 0.5f,
                HeightScale = 1.0f,
                Octaves = 3,
                SeedOffset = 999,
            };

            _humidityNoise = new NativeNoiseConfig
            {
                Frequency = 0.002f,
                Lacunarity = 2.0f,
                Persistence = 0.5f,
                HeightScale = 1.0f,
                Octaves = 3,
                SeedOffset = 1999,
            };
        }

        [Test]
        public void Execute_AssignsValidBiomeIds()
        {
            NativeArray<int> heightMap = new NativeArray<int>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> biomeMap = new NativeArray<byte>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<float> temperatureMap = new NativeArray<float>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<float> humidityMap = new NativeArray<float>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<NativeBiomeData> biomeData = new NativeArray<NativeBiomeData>(
                4, Allocator.TempJob);

            try
            {
                StateId grassId = new StateId(3);
                StateId dirtId = new StateId(4);
                StateId sandId = new StateId(5);
                StateId stoneId = new StateId(1);
                StateId gravelId = new StateId(6);
                StateId sandstoneId = new StateId(7);

                biomeData[0] = new NativeBiomeData
                {
                    BiomeId = 0,
                    TemperatureMin = 0.3f, TemperatureMax = 0.7f, TemperatureCenter = 0.5f,
                    HumidityMin = 0.3f, HumidityMax = 0.7f, HumidityCenter = 0.5f,
                    TopBlock = grassId, FillerBlock = dirtId,
                    StoneBlock = stoneId, UnderwaterBlock = dirtId,
                    FillerDepth = 3, TreeDensity = 0.02f, HeightModifier = 0.0f,
                };
                biomeData[1] = new NativeBiomeData
                {
                    BiomeId = 1,
                    TemperatureMin = 0.3f, TemperatureMax = 0.7f, TemperatureCenter = 0.5f,
                    HumidityMin = 0.5f, HumidityMax = 1.0f, HumidityCenter = 0.75f,
                    TopBlock = grassId, FillerBlock = dirtId,
                    StoneBlock = stoneId, UnderwaterBlock = dirtId,
                    FillerDepth = 3, TreeDensity = 0.15f, HeightModifier = 0.0f,
                };
                biomeData[2] = new NativeBiomeData
                {
                    BiomeId = 2,
                    TemperatureMin = 0.7f, TemperatureMax = 1.0f, TemperatureCenter = 0.85f,
                    HumidityMin = 0.0f, HumidityMax = 0.3f, HumidityCenter = 0.15f,
                    TopBlock = sandId, FillerBlock = sandstoneId,
                    StoneBlock = stoneId, UnderwaterBlock = sandId,
                    FillerDepth = 4, TreeDensity = 0.0f, HeightModifier = 0.0f,
                };
                biomeData[3] = new NativeBiomeData
                {
                    BiomeId = 3,
                    TemperatureMin = 0.0f, TemperatureMax = 0.3f, TemperatureCenter = 0.15f,
                    HumidityMin = 0.0f, HumidityMax = 0.7f, HumidityCenter = 0.35f,
                    TopBlock = stoneId, FillerBlock = gravelId,
                    StoneBlock = stoneId, UnderwaterBlock = gravelId,
                    FillerDepth = 2, TreeDensity = 0.01f, HeightModifier = 0.0f,
                };

                BiomeAssignmentJob job = new BiomeAssignmentJob
                {
                    HeightMap = heightMap,
                    BiomeData = biomeData,
                    Seed = 42L,
                    ChunkCoord = new int3(0, 0, 0),
                    TemperatureNoise = _temperatureNoise,
                    HumidityNoise = _humidityNoise,
                    BiomeMap = biomeMap,
                    TemperatureMap = temperatureMap,
                    HumidityMap = humidityMap,
                };

                job.Schedule().Complete();

                // All biome IDs should be valid (0-3)
                for (int i = 0; i < ChunkConstants.SizeSquared; i++)
                {
                    Assert.LessOrEqual(biomeMap[i], 3, $"Biome ID at {i} should be <= 3");
                }

                // Temperature and humidity should be in [0, 1]
                for (int i = 0; i < ChunkConstants.SizeSquared; i++)
                {
                    Assert.GreaterOrEqual(temperatureMap[i], 0.0f, "Temperature should be >= 0");
                    Assert.LessOrEqual(temperatureMap[i], 1.0f, "Temperature should be <= 1");
                    Assert.GreaterOrEqual(humidityMap[i], 0.0f, "Humidity should be >= 0");
                    Assert.LessOrEqual(humidityMap[i], 1.0f, "Humidity should be <= 1");
                }
            }
            finally
            {
                heightMap.Dispose();
                biomeMap.Dispose();
                temperatureMap.Dispose();
                humidityMap.Dispose();
                biomeData.Dispose();
            }
        }

        [Test]
        public void Execute_Deterministic_SameSeedSameResult()
        {
            NativeArray<int> heightMap = new NativeArray<int>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> biomeMap1 = new NativeArray<byte>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> biomeMap2 = new NativeArray<byte>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<float> tempMap1 = new NativeArray<float>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<float> tempMap2 = new NativeArray<float>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<float> humMap1 = new NativeArray<float>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<float> humMap2 = new NativeArray<float>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<NativeBiomeData> biomeData = new NativeArray<NativeBiomeData>(
                1, Allocator.TempJob);

            try
            {
                biomeData[0] = new NativeBiomeData
                {
                    BiomeId = 0,
                    TemperatureMin = 0.0f, TemperatureMax = 1.0f, TemperatureCenter = 0.5f,
                    HumidityMin = 0.0f, HumidityMax = 1.0f, HumidityCenter = 0.5f,
                    TopBlock = new StateId(3), FillerBlock = new StateId(4),
                    StoneBlock = new StateId(1), UnderwaterBlock = new StateId(4),
                    FillerDepth = 3, TreeDensity = 0.0f, HeightModifier = 0.0f,
                };

                BiomeAssignmentJob job1 = new BiomeAssignmentJob
                {
                    HeightMap = heightMap,
                    BiomeData = biomeData,
                    Seed = 42L,
                    ChunkCoord = new int3(5, 0, 3),
                    TemperatureNoise = _temperatureNoise,
                    HumidityNoise = _humidityNoise,
                    BiomeMap = biomeMap1,
                    TemperatureMap = tempMap1,
                    HumidityMap = humMap1,
                };

                BiomeAssignmentJob job2 = new BiomeAssignmentJob
                {
                    HeightMap = heightMap,
                    BiomeData = biomeData,
                    Seed = 42L,
                    ChunkCoord = new int3(5, 0, 3),
                    TemperatureNoise = _temperatureNoise,
                    HumidityNoise = _humidityNoise,
                    BiomeMap = biomeMap2,
                    TemperatureMap = tempMap2,
                    HumidityMap = humMap2,
                };

                job1.Schedule().Complete();
                job2.Schedule().Complete();

                for (int i = 0; i < ChunkConstants.SizeSquared; i++)
                {
                    Assert.AreEqual(biomeMap1[i], biomeMap2[i],
                        $"Biome assignment should be deterministic at {i}");
                }
            }
            finally
            {
                heightMap.Dispose();
                biomeMap1.Dispose();
                biomeMap2.Dispose();
                tempMap1.Dispose();
                tempMap2.Dispose();
                humMap1.Dispose();
                humMap2.Dispose();
                biomeData.Dispose();
            }
        }
    }
}
