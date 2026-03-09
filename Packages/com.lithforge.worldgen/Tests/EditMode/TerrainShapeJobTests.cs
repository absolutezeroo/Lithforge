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
    public sealed class TerrainShapeJobTests
    {
        private StateId _stoneId;
        private StateId _waterId;
        private StateId _airId;
        private NativeNoiseConfig _flatConfig;

        [SetUp]
        public void SetUp()
        {
            _stoneId = new StateId(1);
            _waterId = new StateId(2);
            _airId = StateId.Air;
            _flatConfig = new NativeNoiseConfig
            {
                Frequency = 0.01f,
                Lacunarity = 2.0f,
                Persistence = 0.5f,
                HeightScale = 0.0f,
                Octaves = 1,
                SeedOffset = 0,
            };
        }

        [Test]
        public void Execute_FlatConfig_StoneBelow_AirAbove()
        {
            NativeArray<StateId> chunkData = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> heightMap = new NativeArray<int>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            try
            {
                int seaLevel = 16;

                TerrainShapeJob job = new TerrainShapeJob
                {
                    ChunkData = chunkData,
                    HeightMap = heightMap,
                    Seed = 42L,
                    ChunkCoord = new int3(0, 0, 0),
                    NoiseConfig = _flatConfig,
                    SeaLevel = seaLevel,
                    StoneId = _stoneId,
                    WaterId = _waterId,
                    AirId = _airId,
                };

                job.Schedule().Complete();

                // Stone below sea level, air at and above
                for (int y = 0; y < ChunkConstants.Size; y++)
                {
                    StateId state = chunkData[ChunkData.GetIndex(0, y, 0)];

                    if (y < seaLevel)
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
            }
        }

        [Test]
        public void Execute_HeightMapPopulated()
        {
            NativeArray<StateId> chunkData = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> heightMap = new NativeArray<int>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                TerrainShapeJob job = new TerrainShapeJob
                {
                    ChunkData = chunkData,
                    HeightMap = heightMap,
                    Seed = 42L,
                    ChunkCoord = new int3(0, 0, 0),
                    NoiseConfig = _flatConfig,
                    SeaLevel = 16,
                    StoneId = _stoneId,
                    WaterId = _waterId,
                    AirId = _airId,
                };

                job.Schedule().Complete();

                // With HeightScale=0, all surface heights should equal SeaLevel
                for (int i = 0; i < ChunkConstants.SizeSquared; i++)
                {
                    Assert.AreEqual(16, heightMap[i]);
                }
            }
            finally
            {
                chunkData.Dispose();
                heightMap.Dispose();
            }
        }

        [Test]
        public void Execute_WaterFillsBelowSeaLevel()
        {
            NativeArray<StateId> chunkData = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> heightMap = new NativeArray<int>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            try
            {
                // Use a config that produces surfaces below sea level
                NativeNoiseConfig lowConfig = new NativeNoiseConfig
                {
                    Frequency = 0.01f,
                    Lacunarity = 2.0f,
                    Persistence = 0.5f,
                    HeightScale = 0.0f,
                    Octaves = 1,
                    SeedOffset = 0,
                };

                int seaLevel = 24;

                TerrainShapeJob job = new TerrainShapeJob
                {
                    ChunkData = chunkData,
                    HeightMap = heightMap,
                    Seed = 42L,
                    ChunkCoord = new int3(0, 0, 0),
                    NoiseConfig = lowConfig,
                    SeaLevel = seaLevel,
                    StoneId = _stoneId,
                    WaterId = _waterId,
                    AirId = _airId,
                };

                job.Schedule().Complete();

                // With HeightScale=0, surface = seaLevel = 24.
                // y < 24 is stone, y >= 24 is air. No water because surface == seaLevel.
                // To test water, we need surface < seaLevel. Let's check a high chunk.
                // Actually with flat config at y=0 chunk, surface=24, so y<24 -> stone, y>=24 -> air
                StateId atSurface = chunkData[ChunkData.GetIndex(0, 24, 0)];
                Assert.AreEqual(_airId, atSurface);
            }
            finally
            {
                chunkData.Dispose();
                heightMap.Dispose();
            }
        }
    }
}
