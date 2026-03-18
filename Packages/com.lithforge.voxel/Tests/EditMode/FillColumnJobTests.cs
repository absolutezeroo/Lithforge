using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Jobs;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace Lithforge.Voxel.Tests
{
    [TestFixture]
    public sealed class FillColumnJobTests
    {
        [Test]
        public void Execute_FillsBelowHeightWithStone()
        {
            NativeArray<StateId> chunkStates = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                StateId stone = new(1);
                StateId air = StateId.Air;
                int surfaceHeight = 16;

                FillColumnJob job = new()
                {
                    ChunkStates = chunkStates,
                    StoneState = stone,
                    AirState = air,
                    SurfaceHeight = surfaceHeight,
                };

                job.Schedule().Complete();

                // Below surface: stone
                for (int y = 0; y < surfaceHeight; y++)
                {
                    StateId state = chunkStates[ChunkData.GetIndex(0, y, 0)];
                    Assert.AreEqual(stone, state, $"Expected stone at y={y}");
                }

                // At and above surface: air
                for (int y = surfaceHeight; y < ChunkConstants.Size; y++)
                {
                    StateId state = chunkStates[ChunkData.GetIndex(0, y, 0)];
                    Assert.AreEqual(air, state, $"Expected air at y={y}");
                }
            }
            finally
            {
                chunkStates.Dispose();
            }
        }

        [Test]
        public void Execute_AllColumnsFilledConsistently()
        {
            NativeArray<StateId> chunkStates = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                StateId stone = new(1);
                int surfaceHeight = 20;

                FillColumnJob job = new()
                {
                    ChunkStates = chunkStates,
                    StoneState = stone,
                    AirState = StateId.Air,
                    SurfaceHeight = surfaceHeight,
                };

                job.Schedule().Complete();

                // Check multiple columns to ensure consistency
                for (int x = 0; x < ChunkConstants.Size; x += 8)
                {
                    for (int z = 0; z < ChunkConstants.Size; z += 8)
                    {
                        // Below: stone
                        StateId below = chunkStates[ChunkData.GetIndex(x, 0, z)];
                        Assert.AreEqual(stone, below, $"Expected stone at ({x},0,{z})");

                        // Above: air
                        StateId above = chunkStates[ChunkData.GetIndex(x, 31, z)];
                        Assert.AreEqual(StateId.Air, above, $"Expected air at ({x},31,{z})");
                    }
                }
            }
            finally
            {
                chunkStates.Dispose();
            }
        }

        [Test]
        public void Execute_SurfaceHeightZero_AllAir()
        {
            NativeArray<StateId> chunkStates = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                FillColumnJob job = new()
                {
                    ChunkStates = chunkStates,
                    StoneState = new StateId(1),
                    AirState = StateId.Air,
                    SurfaceHeight = 0,
                };

                job.Schedule().Complete();

                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    Assert.AreEqual(StateId.Air, chunkStates[i]);
                }
            }
            finally
            {
                chunkStates.Dispose();
            }
        }

        [Test]
        public void Execute_SurfaceHeight32_AllStone()
        {
            NativeArray<StateId> chunkStates = new(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                StateId stone = new(1);

                FillColumnJob job = new()
                {
                    ChunkStates = chunkStates,
                    StoneState = stone,
                    AirState = StateId.Air,
                    SurfaceHeight = ChunkConstants.Size,
                };

                job.Schedule().Complete();

                for (int i = 0; i < ChunkConstants.Volume; i++)
                {
                    Assert.AreEqual(stone, chunkStates[i]);
                }
            }
            finally
            {
                chunkStates.Dispose();
            }
        }
    }
}
