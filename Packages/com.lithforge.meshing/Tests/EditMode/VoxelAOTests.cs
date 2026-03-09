using NUnit.Framework;

namespace Lithforge.Meshing.Tests
{
    [TestFixture]
    public sealed class VoxelAOTests
    {
        [Test]
        public void NoOcclusion_Returns3()
        {
            byte ao = VoxelAO.Compute(false, false, false);
            Assert.AreEqual(3, ao);
        }

        [Test]
        public void CornerOnly_Returns2()
        {
            byte ao = VoxelAO.Compute(false, false, true);
            Assert.AreEqual(2, ao);
        }

        [Test]
        public void OneSide_Returns2()
        {
            byte ao = VoxelAO.Compute(true, false, false);
            Assert.AreEqual(2, ao);
        }

        [Test]
        public void OneSideAndCorner_Returns1()
        {
            byte ao = VoxelAO.Compute(true, false, true);
            Assert.AreEqual(1, ao);
        }

        [Test]
        public void BothSides_Returns0()
        {
            byte ao = VoxelAO.Compute(true, true, false);
            Assert.AreEqual(0, ao);
        }

        [Test]
        public void BothSidesAndCorner_Returns0()
        {
            byte ao = VoxelAO.Compute(true, true, true);
            Assert.AreEqual(0, ao);
        }

        [Test]
        public void TwoSides_NoCorner_Returns1()
        {
            byte ao = VoxelAO.Compute(false, true, true);
            Assert.AreEqual(1, ao);
        }
    }
}
