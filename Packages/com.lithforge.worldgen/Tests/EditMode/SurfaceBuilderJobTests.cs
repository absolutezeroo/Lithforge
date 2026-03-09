using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
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
        private StateId _stoneId;
        private StateId _grassId;
        private StateId _dirtId;

        [SetUp]
        public void SetUp()
        {
            _stoneId = new StateId(1);
            _grassId = new StateId(3);
            _dirtId = new StateId(4);
        }

        [Test]
        public void Execute_GrassOnTop_WhenAboveSeaLevel()
        {
            NativeArray<StateId> chunkData = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> heightMap = new NativeArray<int>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                int surfaceY = 20;

                // Fill stone below surface, air above
                for (int z = 0; z < ChunkConstants.Size; z++)
                {
                    for (int x = 0; x < ChunkConstants.Size; x++)
                    {
                        int columnIndex = z * ChunkConstants.Size + x;
                        heightMap[columnIndex] = surfaceY;

                        for (int y = 0; y < ChunkConstants.Size; y++)
                        {
                            int index = ChunkData.GetIndex(x, y, z);
                            chunkData[index] = y < surfaceY ? _stoneId : StateId.Air;
                        }
                    }
                }

                SurfaceBuilderJob job = new SurfaceBuilderJob
                {
                    ChunkData = chunkData,
                    HeightMap = heightMap,
                    ChunkCoord = new int3(0, 0, 0),
                    SeaLevel = 16,
                    GrassId = _grassId,
                    DirtId = _dirtId,
                    StoneId = _stoneId,
                    AirId = StateId.Air,
                };

                job.Schedule().Complete();

                // Top block (y=19) should be grass (above sea level)
                StateId topState = chunkData[ChunkData.GetIndex(0, 19, 0)];
                Assert.AreEqual(_grassId, topState, "Top block should be grass");

                // Next 3 blocks should be dirt
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
            }
        }

        [Test]
        public void Execute_DirtOnTop_WhenBelowSeaLevel()
        {
            NativeArray<StateId> chunkData = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> heightMap = new NativeArray<int>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                int surfaceY = 10;
                int seaLevel = 16;

                for (int z = 0; z < ChunkConstants.Size; z++)
                {
                    for (int x = 0; x < ChunkConstants.Size; x++)
                    {
                        int columnIndex = z * ChunkConstants.Size + x;
                        heightMap[columnIndex] = surfaceY;

                        for (int y = 0; y < ChunkConstants.Size; y++)
                        {
                            int index = ChunkData.GetIndex(x, y, z);
                            chunkData[index] = y < surfaceY ? _stoneId : StateId.Air;
                        }
                    }
                }

                SurfaceBuilderJob job = new SurfaceBuilderJob
                {
                    ChunkData = chunkData,
                    HeightMap = heightMap,
                    ChunkCoord = new int3(0, 0, 0),
                    SeaLevel = seaLevel,
                    GrassId = _grassId,
                    DirtId = _dirtId,
                    StoneId = _stoneId,
                    AirId = StateId.Air,
                };

                job.Schedule().Complete();

                // Top block (y=9) should be dirt (below sea level, no grass)
                StateId topState = chunkData[ChunkData.GetIndex(0, 9, 0)];
                Assert.AreEqual(_dirtId, topState, "Underwater top block should be dirt, not grass");
            }
            finally
            {
                chunkData.Dispose();
                heightMap.Dispose();
            }
        }

        [Test]
        public void Execute_OnlyReplacesStone()
        {
            NativeArray<StateId> chunkData = new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> heightMap = new NativeArray<int>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                int surfaceY = 20;
                StateId waterId = new StateId(2);

                // Fill with water instead of stone at a specific column
                for (int y = 0; y < surfaceY; y++)
                {
                    int index = ChunkData.GetIndex(5, y, 5);
                    chunkData[index] = waterId;
                }

                heightMap[5 * ChunkConstants.Size + 5] = surfaceY;

                SurfaceBuilderJob job = new SurfaceBuilderJob
                {
                    ChunkData = chunkData,
                    HeightMap = heightMap,
                    ChunkCoord = new int3(0, 0, 0),
                    SeaLevel = 16,
                    GrassId = _grassId,
                    DirtId = _dirtId,
                    StoneId = _stoneId,
                    AirId = StateId.Air,
                };

                job.Schedule().Complete();

                // Water blocks should not be replaced
                StateId state = chunkData[ChunkData.GetIndex(5, 19, 5)];
                Assert.AreEqual(waterId, state, "Non-stone blocks should not be replaced");
            }
            finally
            {
                chunkData.Dispose();
                heightMap.Dispose();
            }
        }
    }
}
