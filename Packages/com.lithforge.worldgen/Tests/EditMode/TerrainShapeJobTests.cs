using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Biome;
using Lithforge.WorldGen.Climate;
using Lithforge.WorldGen.Noise;
using Lithforge.WorldGen.Stages;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Tests
{
    [TestFixture]
    public sealed class TerrainShapeJobTests
    {
        private StateId _stoneId;
        private StateId _waterId;
        private StateId _airId;
        private NativeNoiseConfig _flatTerrainNoise;

        [SetUp]
        public void SetUp()
        {
            _stoneId = new StateId(1);
            _waterId = new StateId(2);
            _airId = StateId.Air;
            _flatTerrainNoise = new NativeNoiseConfig
            {
                Frequency = 0.01f,
                Lacunarity = 2.0f,
                Persistence = 0.5f,
                HeightScale = 0.0f,
                Octaves = 1,
                SeedOffset = 0,
            };
        }

        private NativeArray<ClimateData> CreateUniformClimateMap(
            float temperature, float humidity, float continentalness, float erosion)
        {
            NativeArray<ClimateData> climateMap = new NativeArray<ClimateData>(
                ChunkConstants.SizeSquared, Allocator.TempJob);

            ClimateData uniform = new ClimateData
            {
                Temperature = temperature,
                Humidity = humidity,
                Continentalness = continentalness,
                Erosion = erosion,
            };

            for (int i = 0; i < ChunkConstants.SizeSquared; i++)
            {
                climateMap[i] = uniform;
            }

            return climateMap;
        }

        private NativeArray<NativeBiomeData> CreateSingleBiome(float baseHeight, float heightAmplitude)
        {
            NativeArray<NativeBiomeData> biomeData = new NativeArray<NativeBiomeData>(
                1, Allocator.TempJob);

            biomeData[0] = new NativeBiomeData
            {
                BiomeId = 0,
                TemperatureCenter = 0.5f,
                HumidityCenter = 0.5f,
                ContinentalnessCenter = 0.5f,
                ErosionCenter = 0.5f,
                BaseHeight = baseHeight,
                HeightAmplitude = heightAmplitude,
                TopBlock = new StateId(3),
                FillerBlock = new StateId(4),
                StoneBlock = _stoneId,
                UnderwaterBlock = new StateId(4),
                FillerDepth = 3,
            };

            return biomeData;
        }

        [Test]
        public void Execute_FlatConfig_StoneBelow_AirAbove()
        {
            NativeArray<StateId> chunkData = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> heightMap = new NativeArray<int>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte> biomeMap = new NativeArray<byte>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ClimateData> climateMap = CreateUniformClimateMap(0.5f, 0.5f, 0.5f, 0.5f);
            NativeArray<NativeBiomeData> biomeData = CreateSingleBiome(0.0f, 0.0f);

            try
            {
                int seaLevel = 16;

                TerrainShapeJob job = new TerrainShapeJob
                {
                    ChunkData = chunkData,
                    HeightMap = heightMap,
                    BiomeMap = biomeMap,
                    ClimateMap = climateMap,
                    BiomeData = biomeData,
                    Seed = 42L,
                    ChunkCoord = new int3(0, 0, 0),
                    TerrainNoise = _flatTerrainNoise,
                    SeaLevel = seaLevel,
                    StoneId = _stoneId,
                    WaterId = _waterId,
                    AirId = _airId,
                };

                job.Schedule().Complete();

                // With BaseHeight=0 and HeightAmplitude=0, surface = SeaLevel.
                // Stone at y <= seaLevel, air above.
                for (int y = 0; y < ChunkConstants.Size; y++)
                {
                    StateId state = chunkData[ChunkData.GetIndex(0, y, 0)];

                    if (y <= seaLevel)
                    {
                        Assert.AreEqual(_stoneId, state,
                            $"Expected stone at y={y}");
                    }
                    else
                    {
                        Assert.AreEqual(_airId, state,
                            $"Expected air at y={y}");
                    }
                }
            }
            finally
            {
                chunkData.Dispose();
                heightMap.Dispose();
                biomeMap.Dispose();
                climateMap.Dispose();
                biomeData.Dispose();
            }
        }

        [Test]
        public void Execute_HeightMapPopulated()
        {
            NativeArray<StateId> chunkData = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> heightMap = new NativeArray<int>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> biomeMap = new NativeArray<byte>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ClimateData> climateMap = CreateUniformClimateMap(0.5f, 0.5f, 0.5f, 0.5f);
            NativeArray<NativeBiomeData> biomeData = CreateSingleBiome(0.0f, 0.0f);

            try
            {
                TerrainShapeJob job = new TerrainShapeJob
                {
                    ChunkData = chunkData,
                    HeightMap = heightMap,
                    BiomeMap = biomeMap,
                    ClimateMap = climateMap,
                    BiomeData = biomeData,
                    Seed = 42L,
                    ChunkCoord = new int3(0, 0, 0),
                    TerrainNoise = _flatTerrainNoise,
                    SeaLevel = 16,
                    StoneId = _stoneId,
                    WaterId = _waterId,
                    AirId = _airId,
                };

                job.Schedule().Complete();

                // With BaseHeight=0 and HeightAmplitude=0, all heights = SeaLevel
                for (int i = 0; i < ChunkConstants.SizeSquared; i++)
                {
                    Assert.AreEqual(16, heightMap[i]);
                }
            }
            finally
            {
                chunkData.Dispose();
                heightMap.Dispose();
                biomeMap.Dispose();
                climateMap.Dispose();
                biomeData.Dispose();
            }
        }

        [Test]
        public void Execute_BiomeBlending_DominantBiomeSelected()
        {
            NativeArray<StateId> chunkData = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> heightMap = new NativeArray<int>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte> biomeMap = new NativeArray<byte>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            // Climate at (0.8, 0.1, 0.5, 0.5) — hot, dry — should select desert (biome 2)
            NativeArray<ClimateData> climateMap = CreateUniformClimateMap(0.8f, 0.1f, 0.5f, 0.5f);

            NativeArray<NativeBiomeData> biomeData = new NativeArray<NativeBiomeData>(
                3, Allocator.TempJob);

            try
            {
                // Biome 0: plains (temp=0.5, humidity=0.5)
                biomeData[0] = new NativeBiomeData
                {
                    BiomeId = 0,
                    TemperatureCenter = 0.5f, HumidityCenter = 0.5f,
                    ContinentalnessCenter = 0.5f, ErosionCenter = 0.5f,
                    BaseHeight = 4.0f, HeightAmplitude = 0.0f,
                    TopBlock = new StateId(3), FillerBlock = new StateId(4),
                    StoneBlock = _stoneId, UnderwaterBlock = new StateId(4),
                    FillerDepth = 3,
                };

                // Biome 1: forest (temp=0.5, humidity=0.75)
                biomeData[1] = new NativeBiomeData
                {
                    BiomeId = 1,
                    TemperatureCenter = 0.5f, HumidityCenter = 0.75f,
                    ContinentalnessCenter = 0.5f, ErosionCenter = 0.5f,
                    BaseHeight = 6.0f, HeightAmplitude = 0.0f,
                    TopBlock = new StateId(3), FillerBlock = new StateId(4),
                    StoneBlock = _stoneId, UnderwaterBlock = new StateId(4),
                    FillerDepth = 3,
                };

                // Biome 2: desert (temp=0.85, humidity=0.15) — closest to climate (0.8, 0.1)
                biomeData[2] = new NativeBiomeData
                {
                    BiomeId = 2,
                    TemperatureCenter = 0.85f, HumidityCenter = 0.15f,
                    ContinentalnessCenter = 0.5f, ErosionCenter = 0.5f,
                    BaseHeight = 0.0f, HeightAmplitude = 0.0f,
                    TopBlock = new StateId(5), FillerBlock = new StateId(7),
                    StoneBlock = _stoneId, UnderwaterBlock = new StateId(5),
                    FillerDepth = 4,
                };

                TerrainShapeJob job = new TerrainShapeJob
                {
                    ChunkData = chunkData,
                    HeightMap = heightMap,
                    BiomeMap = biomeMap,
                    ClimateMap = climateMap,
                    BiomeData = biomeData,
                    Seed = 42L,
                    ChunkCoord = new int3(0, 0, 0),
                    TerrainNoise = _flatTerrainNoise,
                    SeaLevel = 64,
                    StoneId = _stoneId,
                    WaterId = _waterId,
                    AirId = _airId,
                };

                job.Schedule().Complete();

                // All columns should select desert (biome 2) as dominant
                for (int i = 0; i < ChunkConstants.SizeSquared; i++)
                {
                    Assert.AreEqual(2, biomeMap[i],
                        $"Expected desert biome (2) at column {i}");
                }
            }
            finally
            {
                chunkData.Dispose();
                heightMap.Dispose();
                biomeMap.Dispose();
                climateMap.Dispose();
                biomeData.Dispose();
            }
        }
    }
}
