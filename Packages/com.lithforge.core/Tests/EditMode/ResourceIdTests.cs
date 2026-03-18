using System;
using Lithforge.Core.Data;
using NUnit.Framework;

namespace Lithforge.Core.Tests
{
    [TestFixture]
    public sealed class ResourceIdTests
    {
        [Test]
        public void Constructor_ValidNamespaceAndName_CreatesResourceId()
        {
            ResourceId id = new("lithforge", "stone");

            Assert.AreEqual("lithforge", id.Namespace);
            Assert.AreEqual("stone", id.Name);
        }

        [Test]
        public void Constructor_NullNamespace_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new ResourceId(null, "stone"));
        }

        [Test]
        public void Constructor_EmptyName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new ResourceId("lithforge", ""));
        }

        [Test]
        public void Parse_ValidString_ReturnsResourceId()
        {
            ResourceId id = ResourceId.Parse("lithforge:stone");

            Assert.AreEqual("lithforge", id.Namespace);
            Assert.AreEqual("stone", id.Name);
        }

        [Test]
        public void Parse_WithSlashes_ReturnsResourceId()
        {
            ResourceId id = ResourceId.Parse("lithforge:blocks/stone");

            Assert.AreEqual("lithforge", id.Namespace);
            Assert.AreEqual("blocks/stone", id.Name);
        }

        [Test]
        public void Parse_InvalidFormat_UpperCase_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ResourceId.Parse("Lithforge:Stone"));
        }

        [Test]
        public void Parse_InvalidFormat_NoColon_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ResourceId.Parse("lithforgestone"));
        }

        [Test]
        public void Parse_InvalidFormat_Spaces_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ResourceId.Parse("lith forge:stone"));
        }

        [Test]
        public void Parse_NullString_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => ResourceId.Parse(null));
        }

        [Test]
        public void TryParse_ValidString_ReturnsTrueAndResult()
        {
            bool success = ResourceId.TryParse("lithforge:oak_log", out ResourceId id);

            Assert.IsTrue(success);
            Assert.AreEqual("lithforge", id.Namespace);
            Assert.AreEqual("oak_log", id.Name);
        }

        [Test]
        public void TryParse_InvalidString_ReturnsFalse()
        {
            bool success = ResourceId.TryParse("INVALID", out ResourceId _);

            Assert.IsFalse(success);
        }

        [Test]
        public void ToString_ReturnsNamespaceColonName()
        {
            ResourceId id = new("lithforge", "stone");

            Assert.AreEqual("lithforge:stone", id.ToString());
        }

        [Test]
        public void Equals_SameValues_ReturnsTrue()
        {
            ResourceId a = new("lithforge", "stone");
            ResourceId b = new("lithforge", "stone");

            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
        }

        [Test]
        public void Equals_DifferentValues_ReturnsFalse()
        {
            ResourceId a = new("lithforge", "stone");
            ResourceId b = new("lithforge", "dirt");

            Assert.IsFalse(a.Equals(b));
            Assert.IsTrue(a != b);
        }

        [Test]
        public void GetHashCode_SameValues_SameHash()
        {
            ResourceId a = new("lithforge", "stone");
            ResourceId b = new("lithforge", "stone");

            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }
    }
}
