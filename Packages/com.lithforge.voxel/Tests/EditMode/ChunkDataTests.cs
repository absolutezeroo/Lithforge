using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using NUnit.Framework;

using Unity.Collections;

namespace Lithforge.Voxel.Tests
{
    [TestFixture]
    public sealed class ChunkDataTests
    {
        [Test]
        public void GetIndex_Origin_ReturnsZero()
        {
            int index = ChunkData.GetIndex(0, 0, 0);

            Assert.AreEqual(0, index);
        }

        [Test]
        public void GetIndex_MaxCorner_ReturnsLastIndex()
        {
            int index = ChunkData.GetIndex(31, 31, 31);

            Assert.AreEqual(ChunkConstants.Volume - 1, index);
        }

        [Test]
        public void GetIndex_YMajor_YIncreasesBy1024()
        {
            int index0 = ChunkData.GetIndex(0, 0, 0);
            int index1 = ChunkData.GetIndex(0, 1, 0);

            Assert.AreEqual(ChunkConstants.SizeSquared, index1 - index0);
        }

        [Test]
        public void GetIndex_ZIncreasesBySize()
        {
            int index0 = ChunkData.GetIndex(0, 0, 0);
            int index1 = ChunkData.GetIndex(0, 0, 1);

            Assert.AreEqual(ChunkConstants.Size, index1 - index0);
        }

        [Test]
        public void SetAndGetState_RoundTrip()
        {
            NativeArray<StateId> states = new(
                ChunkConstants.Volume, Allocator.TempJob);
            ChunkData chunk = new(states);

            try
            {
                StateId stone = new(1);
                chunk.SetState(5, 10, 15, stone);
                StateId result = chunk.GetState(5, 10, 15);

                Assert.AreEqual(stone, result);
            }
            finally
            {
                chunk.Dispose();
            }
        }

        [Test]
        public void NewChunk_AllAir()
        {
            NativeArray<StateId> states = new(
                ChunkConstants.Volume, Allocator.TempJob);
            ChunkData chunk = new(states);

            try
            {
                for (int i = 0; i < 100; i++)
                {
                    Assert.AreEqual(StateId.Air, chunk.GetState(i % 32, i / 32 % 32, i / 1024 % 32));
                }
            }
            finally
            {
                chunk.Dispose();
            }
        }

        [Test]
        public void Dispose_DisposesNativeArray()
        {
            NativeArray<StateId> states = new(
                ChunkConstants.Volume, Allocator.TempJob);
            ChunkData chunk = new(states);

            chunk.Dispose();

            Assert.IsFalse(states.IsCreated);
        }
    }
}
