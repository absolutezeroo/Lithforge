using Lithforge.WorldGen.Lighting;
using NUnit.Framework;

namespace Lithforge.WorldGen.Tests
{
    [TestFixture]
    public sealed class LightUtilsTests
    {
        [Test]
        public void Pack_SunAndBlock_EncodesCorrectly()
        {
            byte packed = LightUtils.Pack(15, 14);

            Assert.AreEqual(15, LightUtils.GetSunLight(packed));
            Assert.AreEqual(14, LightUtils.GetBlockLight(packed));
        }

        [Test]
        public void Pack_ZeroValues_ReturnsZero()
        {
            byte packed = LightUtils.Pack(0, 0);

            Assert.AreEqual(0, packed);
            Assert.AreEqual(0, LightUtils.GetSunLight(packed));
            Assert.AreEqual(0, LightUtils.GetBlockLight(packed));
        }

        [Test]
        public void Pack_MaxSunZeroBlock_HighNibbleOnly()
        {
            byte packed = LightUtils.Pack(15, 0);

            Assert.AreEqual(0xF0, packed);
            Assert.AreEqual(15, LightUtils.GetSunLight(packed));
            Assert.AreEqual(0, LightUtils.GetBlockLight(packed));
        }

        [Test]
        public void Pack_ZeroSunMaxBlock_LowNibbleOnly()
        {
            byte packed = LightUtils.Pack(0, 15);

            Assert.AreEqual(0x0F, packed);
            Assert.AreEqual(0, LightUtils.GetSunLight(packed));
            Assert.AreEqual(15, LightUtils.GetBlockLight(packed));
        }

        [Test]
        public void GetSunLight_ExtractsHighNibble()
        {
            // 0xA5 = 1010_0101 -> sun = 10, block = 5
            byte packed = 0xA5;

            Assert.AreEqual(10, LightUtils.GetSunLight(packed));
        }

        [Test]
        public void GetBlockLight_ExtractsLowNibble()
        {
            // 0xA5 = 1010_0101 -> sun = 10, block = 5
            byte packed = 0xA5;

            Assert.AreEqual(5, LightUtils.GetBlockLight(packed));
        }

        [Test]
        public void Pack_BlockMaskedToFourBits()
        {
            // Block value > 15 should be masked to low 4 bits
            byte packed = LightUtils.Pack(7, 0xFF);

            Assert.AreEqual(15, LightUtils.GetBlockLight(packed));
            Assert.AreEqual(7, LightUtils.GetSunLight(packed));
        }

        [Test]
        public void RoundTrip_AllCombinations()
        {
            for (byte sun = 0; sun < 16; sun++)
            {
                for (byte block = 0; block < 16; block++)
                {
                    byte packed = LightUtils.Pack(sun, block);

                    Assert.AreEqual(sun, LightUtils.GetSunLight(packed),
                        $"Sun mismatch at sun={sun}, block={block}");
                    Assert.AreEqual(block, LightUtils.GetBlockLight(packed),
                        $"Block mismatch at sun={sun}, block={block}");
                }
            }
        }
    }
}
