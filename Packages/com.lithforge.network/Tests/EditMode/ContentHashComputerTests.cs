using Lithforge.Core.Data;
using Lithforge.Network;
using Lithforge.Voxel.Block;
using NUnit.Framework;

namespace Lithforge.Network.Tests
{
    [TestFixture]
    public sealed class ContentHashComputerTests
    {
        [Test]
        public void Compute_SameRegistry_ReturnsSameHash()
        {
            StateRegistry registry1 = CreateTestRegistry();
            StateRegistry registry2 = CreateTestRegistry();

            ContentHash hash1 = ContentHashComputer.Compute(registry1);
            ContentHash hash2 = ContentHashComputer.Compute(registry2);

            Assert.AreEqual(hash1, hash2, "Same content should produce identical hashes");
        }

        [Test]
        public void Compute_DifferentRegistry_ReturnsDifferentHash()
        {
            StateRegistry registry1 = CreateTestRegistry();

            StateRegistry registry2 = new();
            registry2.Register(new BlockRegistrationData(
                id: ResourceId.Parse("lithforge:different_block"),
                stateCount: 1,
                renderLayer: "opaque",
                collisionShape: "full_cube",
                lightEmission: 0,
                lightFilter: 15,
                mapColor: "#FF0000",
                lootTable: null,
                hardness: 5.0f,
                blastResistance: 5.0f,
                requiresTool: true,
                materialType: BlockMaterialType.Stone,
                requiredToolLevel: 1));

            ContentHash hash1 = ContentHashComputer.Compute(registry1);
            ContentHash hash2 = ContentHashComputer.Compute(registry2);

            Assert.AreNotEqual(hash1, hash2, "Different content should produce different hashes");
        }

        [Test]
        public void Compute_EmptyRegistry_ReturnsNonZeroHash()
        {
            StateRegistry registry = new();
            ContentHash hash = ContentHashComputer.Compute(registry);

            // Even an empty registry has the AIR state (StateId 0)
            Assert.AreNotEqual(ContentHash.Empty, hash, "Empty registry should still produce a non-empty hash");
        }

        [Test]
        public void Compute_IsDeterministic_AcrossMultipleCalls()
        {
            StateRegistry registry = CreateTestRegistry();

            ContentHash hash1 = ContentHashComputer.Compute(registry);
            ContentHash hash2 = ContentHashComputer.Compute(registry);
            ContentHash hash3 = ContentHashComputer.Compute(registry);

            Assert.AreEqual(hash1, hash2);
            Assert.AreEqual(hash2, hash3);
        }

        [Test]
        public void ContentHash_ToString_Returns32HexChars()
        {
            ContentHash hash = new(0x0123456789ABCDEF, 0xFEDCBA9876543210);
            string hex = hash.ToString();

            Assert.AreEqual(32, hex.Length);
            Assert.AreEqual("0123456789abcdeffedcba9876543210", hex);
        }

        [Test]
        public void ContentHash_Equality_WorksCorrectly()
        {
            ContentHash a = new(1, 2);
            ContentHash b = new(1, 2);
            ContentHash c = new(1, 3);

            Assert.IsTrue(a == b);
            Assert.IsTrue(a.Equals(b));
            Assert.IsFalse(a == c);
            Assert.IsFalse(a != b);
            Assert.IsTrue(a != c);
        }

        private static StateRegistry CreateTestRegistry()
        {
            StateRegistry registry = new();
            registry.Register(new BlockRegistrationData(
                id: ResourceId.Parse("lithforge:stone"),
                stateCount: 1,
                renderLayer: "opaque",
                collisionShape: "full_cube",
                lightEmission: 0,
                lightFilter: 15,
                mapColor: "#808080",
                lootTable: null,
                hardness: 1.5f,
                blastResistance: 6.0f,
                requiresTool: true,
                materialType: BlockMaterialType.Stone,
                requiredToolLevel: 0));
            return registry;
        }
    }
}
