using Lithforge.Voxel.Block;
using NUnit.Framework;

namespace Lithforge.Voxel.Tests
{
    [TestFixture]
    public sealed class StateIdTests
    {
        [Test]
        public void Air_HasValueZero()
        {
            Assert.AreEqual(0, StateId.Air.Value);
        }

        [Test]
        public void Constructor_SetsValue()
        {
            StateId id = new StateId(42);

            Assert.AreEqual(42, id.Value);
        }

        [Test]
        public void Equals_SameValue_ReturnsTrue()
        {
            StateId a = new StateId(5);
            StateId b = new StateId(5);

            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
        }

        [Test]
        public void Equals_DifferentValue_ReturnsFalse()
        {
            StateId a = new StateId(5);
            StateId b = new StateId(6);

            Assert.IsFalse(a.Equals(b));
            Assert.IsTrue(a != b);
        }

        [Test]
        public void GetHashCode_SameValue_SameHash()
        {
            StateId a = new StateId(10);
            StateId b = new StateId(10);

            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void ToString_ReturnsExpectedFormat()
        {
            StateId id = new StateId(7);

            Assert.AreEqual("StateId(7)", id.ToString());
        }

        [Test]
        public void DefaultValue_IsAir()
        {
            StateId id = default;

            Assert.AreEqual(StateId.Air, id);
        }
    }
}
