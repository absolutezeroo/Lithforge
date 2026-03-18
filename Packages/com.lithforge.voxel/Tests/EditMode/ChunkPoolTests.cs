using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using NUnit.Framework;
using Unity.Collections;

namespace Lithforge.Voxel.Tests
{
    [TestFixture]
    public sealed class ChunkPoolTests
    {
        [Test]
        public void Constructor_PreAllocates()
        {
            ChunkPool pool = new(4);

            try
            {
                Assert.AreEqual(4, pool.AvailableCount);
                Assert.AreEqual(4, pool.TotalAllocated);
            }
            finally
            {
                pool.Dispose();
            }
        }

        [Test]
        public void Checkout_ReturnsArrayOfCorrectSize()
        {
            ChunkPool pool = new(1);

            try
            {
                NativeArray<StateId> array = pool.Checkout();

                Assert.AreEqual(ChunkConstants.Volume, array.Length);
                Assert.AreEqual(0, pool.AvailableCount);

                pool.Return(array);
            }
            finally
            {
                pool.Dispose();
            }
        }

        [Test]
        public void Checkout_AllAir()
        {
            ChunkPool pool = new(1);

            try
            {
                NativeArray<StateId> array = pool.Checkout();

                for (int i = 0; i < 100; i++)
                {
                    Assert.AreEqual(StateId.Air, array[i]);
                }

                pool.Return(array);
            }
            finally
            {
                pool.Dispose();
            }
        }

        [Test]
        public void Return_ClearsArray()
        {
            ChunkPool pool = new(1);

            try
            {
                NativeArray<StateId> array = pool.Checkout();
                array[0] = new StateId(42);
                pool.Return(array);

                NativeArray<StateId> reused = pool.Checkout();
                Assert.AreEqual(StateId.Air, reused[0]);
                pool.Return(reused);
            }
            finally
            {
                pool.Dispose();
            }
        }

        [Test]
        public void Checkout_PoolExhausted_AllocatesNew()
        {
            ChunkPool pool = new(1);

            try
            {
                NativeArray<StateId> first = pool.Checkout();
                NativeArray<StateId> second = pool.Checkout();

                Assert.AreEqual(ChunkConstants.Volume, second.Length);
                Assert.AreEqual(2, pool.TotalAllocated);

                pool.Return(first);
                pool.Return(second);
            }
            finally
            {
                pool.Dispose();
            }
        }

        [Test]
        public void Dispose_DisposesAllArrays()
        {
            ChunkPool pool = new(2);
            pool.Dispose();

            Assert.AreEqual(0, pool.AvailableCount);
        }

        [Test]
        public void Dispose_DisposesCheckedOutArrays()
        {
            ChunkPool pool = new(2);
            NativeArray<StateId> checkedOut = pool.Checkout();

            Assert.AreEqual(1, pool.CheckedOutCount);

            // Dispose without returning — should not leak
            pool.Dispose();

            Assert.IsFalse(checkedOut.IsCreated);
        }

        [Test]
        public void Return_MultipleCheckouts_TracksCorrectly()
        {
            ChunkPool pool = new(4);

            try
            {
                NativeArray<StateId> a = pool.Checkout();
                NativeArray<StateId> b = pool.Checkout();
                NativeArray<StateId> c = pool.Checkout();

                Assert.AreEqual(3, pool.CheckedOutCount);
                Assert.AreEqual(1, pool.AvailableCount);

                pool.Return(b);
                Assert.AreEqual(2, pool.CheckedOutCount);
                Assert.AreEqual(2, pool.AvailableCount);

                pool.Return(a);
                Assert.AreEqual(1, pool.CheckedOutCount);

                pool.Return(c);
                Assert.AreEqual(0, pool.CheckedOutCount);
                Assert.AreEqual(4, pool.AvailableCount);
            }
            finally
            {
                pool.Dispose();
            }
        }
    }
}
