using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Climate;
using Lithforge.WorldGen.Noise;
using Lithforge.WorldGen.Stages;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Tests
{
    /// <summary>
    /// Tests for ClimateNoiseJob which replaced BiomeAssignmentJob.
    /// Verifies that climate noise sampling produces valid, deterministic results.
    /// </summary>
    [TestFixture]
    public sealed class ClimateNoiseJobTests
    {
        private NativeNoiseConfig _temperatureNoise;
        private NativeNoiseConfig _humidityNoise;
        private NativeNoiseConfig _continentalnessNoise;
        private NativeNoiseConfig _erosionNoise;

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

            _continentalnessNoise = new NativeNoiseConfig
            {
                Frequency = 0.002f,
                Lacunarity = 2.0f,
                Persistence = 0.55f,
                HeightScale = 1.0f,
                Octaves = 4,
                SeedOffset = 2999,
            };

            _erosionNoise = new NativeNoiseConfig
            {
                Frequency = 0.003f,
                Lacunarity = 2.0f,
                Persistence = 0.5f,
                HeightScale = 1.0f,
                Octaves = 3,
                SeedOffset = 3999,
            };
        }

        [Test]
        public void Execute_AllClimateValuesInRange()
        {
            NativeArray<ClimateData> climateMap = new NativeArray<ClimateData>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                ClimateNoiseJob job = new ClimateNoiseJob
                {
                    ClimateMap = climateMap,
                    Seed = 42L,
                    ChunkCoord = new int3(0, 0, 0),
                    TemperatureNoise = _temperatureNoise,
                    HumidityNoise = _humidityNoise,
                    ContinentalnessNoise = _continentalnessNoise,
                    ErosionNoise = _erosionNoise,
                };

                job.Schedule().Complete();

                for (int i = 0; i < ChunkConstants.SizeSquared; i++)
                {
                    ClimateData climate = climateMap[i];
                    Assert.GreaterOrEqual(climate.Temperature, 0.0f, $"Temperature at {i} should be >= 0");
                    Assert.LessOrEqual(climate.Temperature, 1.0f, $"Temperature at {i} should be <= 1");
                    Assert.GreaterOrEqual(climate.Humidity, 0.0f, $"Humidity at {i} should be >= 0");
                    Assert.LessOrEqual(climate.Humidity, 1.0f, $"Humidity at {i} should be <= 1");
                    Assert.GreaterOrEqual(climate.Continentalness, 0.0f, $"Continentalness at {i} should be >= 0");
                    Assert.LessOrEqual(climate.Continentalness, 1.0f, $"Continentalness at {i} should be <= 1");
                    Assert.GreaterOrEqual(climate.Erosion, 0.0f, $"Erosion at {i} should be >= 0");
                    Assert.LessOrEqual(climate.Erosion, 1.0f, $"Erosion at {i} should be <= 1");
                }
            }
            finally
            {
                climateMap.Dispose();
            }
        }

        [Test]
        public void Execute_Deterministic_SameSeedSameResult()
        {
            NativeArray<ClimateData> climateMap1 = new NativeArray<ClimateData>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ClimateData> climateMap2 = new NativeArray<ClimateData>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                ClimateNoiseJob job1 = new ClimateNoiseJob
                {
                    ClimateMap = climateMap1,
                    Seed = 42L,
                    ChunkCoord = new int3(5, 0, 3),
                    TemperatureNoise = _temperatureNoise,
                    HumidityNoise = _humidityNoise,
                    ContinentalnessNoise = _continentalnessNoise,
                    ErosionNoise = _erosionNoise,
                };

                ClimateNoiseJob job2 = new ClimateNoiseJob
                {
                    ClimateMap = climateMap2,
                    Seed = 42L,
                    ChunkCoord = new int3(5, 0, 3),
                    TemperatureNoise = _temperatureNoise,
                    HumidityNoise = _humidityNoise,
                    ContinentalnessNoise = _continentalnessNoise,
                    ErosionNoise = _erosionNoise,
                };

                job1.Schedule().Complete();
                job2.Schedule().Complete();

                for (int i = 0; i < ChunkConstants.SizeSquared; i++)
                {
                    Assert.AreEqual(climateMap1[i].Temperature, climateMap2[i].Temperature,
                        $"Temperature should be deterministic at {i}");
                    Assert.AreEqual(climateMap1[i].Humidity, climateMap2[i].Humidity,
                        $"Humidity should be deterministic at {i}");
                    Assert.AreEqual(climateMap1[i].Continentalness, climateMap2[i].Continentalness,
                        $"Continentalness should be deterministic at {i}");
                    Assert.AreEqual(climateMap1[i].Erosion, climateMap2[i].Erosion,
                        $"Erosion should be deterministic at {i}");
                }
            }
            finally
            {
                climateMap1.Dispose();
                climateMap2.Dispose();
            }
        }
    }
}
