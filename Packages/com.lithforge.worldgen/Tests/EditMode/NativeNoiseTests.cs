using Lithforge.WorldGen.Noise;
using NUnit.Framework;

namespace Lithforge.WorldGen.Tests
{
    [TestFixture]
    public sealed class NativeNoiseTests
    {
        private NativeNoiseConfig _defaultConfig;

        [SetUp]
        public void SetUp()
        {
            _defaultConfig = new NativeNoiseConfig
            {
                Frequency = 0.01f,
                Lacunarity = 2.0f,
                Persistence = 0.5f,
                HeightScale = 16.0f,
                Octaves = 4,
                SeedOffset = 0,
            };
        }

        [Test]
        public void Sample2D_Deterministic_SameSeedSameResult()
        {
            float result1 = NativeNoise.Sample2D(100.0f, 200.0f, _defaultConfig, 12345L);
            float result2 = NativeNoise.Sample2D(100.0f, 200.0f, _defaultConfig, 12345L);

            Assert.AreEqual(result1, result2);
        }

        [Test]
        public void Sample2D_DifferentSeeds_DifferentResults()
        {
            float result1 = NativeNoise.Sample2D(100.0f, 200.0f, _defaultConfig, 12345L);
            float result2 = NativeNoise.Sample2D(100.0f, 200.0f, _defaultConfig, 54321L);

            Assert.AreNotEqual(result1, result2);
        }

        [Test]
        public void Sample2D_OutputInRange()
        {
            for (int x = 0; x < 100; x++)
            {
                for (int z = 0; z < 100; z++)
                {
                    float value = NativeNoise.Sample2D(x, z, _defaultConfig, 42L);

                    Assert.GreaterOrEqual(value, -_defaultConfig.HeightScale,
                        $"Value {value} at ({x},{z}) below -{_defaultConfig.HeightScale}");
                    Assert.LessOrEqual(value, _defaultConfig.HeightScale,
                        $"Value {value} at ({x},{z}) above {_defaultConfig.HeightScale}");
                }
            }
        }

        [Test]
        public void Sample3D_Deterministic_SameSeedSameResult()
        {
            float result1 = NativeNoise.Sample3D(10.0f, 20.0f, 30.0f, _defaultConfig, 12345L);
            float result2 = NativeNoise.Sample3D(10.0f, 20.0f, 30.0f, _defaultConfig, 12345L);

            Assert.AreEqual(result1, result2);
        }

        [Test]
        public void Sample2D_DifferentPositions_VaryingResults()
        {
            float result1 = NativeNoise.Sample2D(0.0f, 0.0f, _defaultConfig, 42L);
            float result2 = NativeNoise.Sample2D(500.0f, 500.0f, _defaultConfig, 42L);

            Assert.AreNotEqual(result1, result2);
        }
    }
}
