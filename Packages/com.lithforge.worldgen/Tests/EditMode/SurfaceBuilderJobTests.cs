using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Biome;
using Lithforge.WorldGen.Stages;

using NUnit.Framework;

using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Tests
{
    [TestFixture]
    public sealed class SurfaceBuilderJobTests
    {
        [SetUp]
        public void SetUp()
        {
            _stoneId = new StateId(1);
            _grassId = new StateId(3);
            _dirtId = new StateId(4);
            _sandId = new StateId(5);
        }
        private StateId _stoneId;
        private StateId _grassId;
        private StateId _dirtId;
        private StateId _sandId;

        private NativeBiomeData CreatePlainsBiome()
        {
            return new NativeBiomeData
            {
                BiomeId = 0,
                TemperatureMin = 0.3f,
                TemperatureMax = 0.7f,
                TemperatureCenter = 0.5f,
                HumidityMin = 0.3f,
                HumidityMax = 0.7f,
                HumidityCenter = 0.5f,
                TopBlock = _grassId,
                FillerBlock = _dirtId,
                StoneBlock = _stoneId,
                UnderwaterBlock = _dirtId,
                FillerDepth = 3,
                TreeDensity = 0.02f,
            };
        }

        [Test]
        public void Execute_GrassOnTop_WhenAboveSeaLevel()
        {
            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<int> heightMap = new(
                ChunkConstants.SizeSquared, Allocator.TempJob);
            NativeArray<byte> biomeMap = new(
                ChunkConstants.SizeSquared, Allocator.TempJob);
            NativeArray<NativeBiomeData> biomeData = new(
                1, Allocator.TempJob);

            try
            {
                biomeData[0] = CreatePlainsBiome();

                int surfaceY = 20;

                // Fill stone below surface, air above
                for (int z = 0; z < ChunkConstants.Size; z++)
                {
                    for (int x = 0; x < ChunkConstants.Size; x++)
                    {
                        int columnIndex = z * ChunkConstants.Size + x;
                        heightMap[columnIndex] = surfaceY;
                        biomeMap[columnIndex] = 0;

                        for (int y = 0; y < ChunkConstants.Size; y++)
                        {
                            int index = ChunkData.GetIndex(x, y, z);
                            chunkData[index] = y < surfaceY ? _stoneId : StateId.Air;
                        }
                    }
                }

                SurfaceBuilderJob job = new()
                {
                    ChunkData = chunkData,
                    HeightMap = heightMap,
                    BiomeMap = biomeMap,
                    BiomeData = biomeData,
                    ChunkCoord = new int3(0, 0, 0),
                    SeaLevel = 16,
                    StoneId = _stoneId,
                    AirId = StateId.Air,
                };

                job.Schedule(ChunkConstants.SizeSquared, 32).Complete();

                // Top block (y=19) should be grass (above sea level)
                StateId topState = chunkData[ChunkData.GetIndex(0, 19, 0)];
                Assert.AreEqual(_grassId, topState, "Top block should be grass");

                // Next 3 blocks should be dirt (filler)
                for (int d = 1; d <= 3; d++)
                {
                    StateId dirtState = chunkData[ChunkData.GetIndex(0, 19 - d, 0)];
                    Assert.AreEqual(_dirtId, dirtState,
                        $"Block at depth {d} should be dirt");
                }

                // Below dirt should still be stone
                StateId deepState = chunkData[ChunkData.GetIndex(0, 15, 0)];
                Assert.AreEqual(_stoneId, deepState, "Deep block should be stone");
            }
            finally
            {
                chunkData.Dispose();
                heightMap.Dispose();
                biomeMap.Dispose();
                biomeData.Dispose();
            }
        }

        [Test]
        public void Execute_UnderwaterBlock_WhenBelowSeaLevel()
        {
            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<int> heightMap = new(
                ChunkConstants.SizeSquared, Allocator.TempJob);
            NativeArray<byte> biomeMap = new(
                ChunkConstants.SizeSquared, Allocator.TempJob);
            NativeArray<NativeBiomeData> biomeData = new(
                1, Allocator.TempJob);

            try
            {
                biomeData[0] = CreatePlainsBiome();

                int surfaceY = 10;
                int seaLevel = 16;

                for (int z = 0; z < ChunkConstants.Size; z++)
                {
                    for (int x = 0; x < ChunkConstants.Size; x++)
                    {
                        int columnIndex = z * ChunkConstants.Size + x;
                        heightMap[columnIndex] = surfaceY;
                        biomeMap[columnIndex] = 0;

                        for (int y = 0; y < ChunkConstants.Size; y++)
                        {
                            int index = ChunkData.GetIndex(x, y, z);
                            chunkData[index] = y < surfaceY ? _stoneId : StateId.Air;
                        }
                    }
                }

                SurfaceBuilderJob job = new()
                {
                    ChunkData = chunkData,
                    HeightMap = heightMap,
                    BiomeMap = biomeMap,
                    BiomeData = biomeData,
                    ChunkCoord = new int3(0, 0, 0),
                    SeaLevel = seaLevel,
                    StoneId = _stoneId,
                    AirId = StateId.Air,
                };

                job.Schedule(ChunkConstants.SizeSquared, 32).Complete();

                // Top block (y=9) should be UnderwaterBlock (dirt for plains, below sea level)
                StateId topState = chunkData[ChunkData.GetIndex(0, 9, 0)];
                Assert.AreEqual(_dirtId, topState, "Underwater top block should be dirt, not grass");
            }
            finally
            {
                chunkData.Dispose();
                heightMap.Dispose();
                biomeMap.Dispose();
                biomeData.Dispose();
            }
        }

        [Test]
        public void Execute_OnlyReplacesStone()
        {
            NativeArray<StateId> chunkData = new(
                ChunkConstants.Volume, Allocator.TempJob);
            NativeArray<int> heightMap = new(
                ChunkConstants.SizeSquared, Allocator.TempJob);
            NativeArray<byte> biomeMap = new(
                ChunkConstants.SizeSquared, Allocator.TempJob);
            NativeArray<NativeBiomeData> biomeData = new(
                1, Allocator.TempJob);

            try
            {
                biomeData[0] = CreatePlainsBiome();

                int surfaceY = 20;
                StateId waterId = new(2);

                // Fill with water instead of stone at a specific column
                for (int y = 0; y < surfaceY; y++)
                {
                    int index = ChunkData.GetIndex(5, y, 5);
                    chunkData[index] = waterId;
                }

                heightMap[5 * ChunkConstants.Size + 5] = surfaceY;
                biomeMap[5 * ChunkConstants.Size + 5] = 0;

                SurfaceBuilderJob job = new()
                {
                    ChunkData = chunkData,
                    HeightMap = heightMap,
                    BiomeMap = biomeMap,
                    BiomeData = biomeData,
                    ChunkCoord = new int3(0, 0, 0),
                    SeaLevel = 16,
                    StoneId = _stoneId,
                    AirId = StateId.Air,
                };

                job.Schedule(ChunkConstants.SizeSquared, 32).Complete();

                // Water blocks should not be replaced
                StateId state = chunkData[ChunkData.GetIndex(5, 19, 5)];
                Assert.AreEqual(waterId, state, "Non-stone blocks should not be replaced");
            }
            finally
            {
                chunkData.Dispose();
                heightMap.Dispose();
                biomeMap.Dispose();
                biomeData.Dispose();
            }
        }
    }
}
