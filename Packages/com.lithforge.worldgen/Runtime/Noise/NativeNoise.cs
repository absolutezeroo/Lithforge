using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Noise
{
    /// <summary>Burst-compatible fractal noise sampling using Unity.Mathematics simplex and Perlin functions.</summary>
    public static class NativeNoise
    {
        /// <summary>Samples 2D fractal simplex noise at (x, z) with the given config and world seed.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sample2D(float x, float z, NativeNoiseConfig config, long seed)
        {
            float amplitude = 1.0f;
            float frequency = config.Frequency;
            float sum = 0.0f;
            float maxAmplitude = 0.0f;

            float seedX = (seed & 0xFFFF) * 0.3183099f + config.SeedOffset;
            float seedZ = ((seed >> 16) & 0xFFFF) * 0.3183099f + config.SeedOffset;

            for (int i = 0; i < config.Octaves; i++)
            {
                float sampleX = (x + seedX) * frequency;
                float sampleZ = (z + seedZ) * frequency;

                float noiseValue = noise.snoise(new float2(sampleX, sampleZ));
                sum += noiseValue * amplitude;
                maxAmplitude += amplitude;

                amplitude *= config.Persistence;
                frequency *= config.Lacunarity;
            }

            return (sum / maxAmplitude) * config.HeightScale;
        }

        /// <summary>Samples 3D fractal simplex noise at (x, y, z) with the given config and world seed.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sample3D(float x, float y, float z, NativeNoiseConfig config, long seed)
        {
            float amplitude = 1.0f;
            float frequency = config.Frequency;
            float sum = 0.0f;
            float maxAmplitude = 0.0f;

            float seedX = (seed & 0xFFFF) * 0.3183099f + config.SeedOffset;
            float seedY = ((seed >> 16) & 0xFFFF) * 0.3183099f + config.SeedOffset;
            float seedZ = ((seed >> 32) & 0xFFFF) * 0.3183099f + config.SeedOffset;

            for (int i = 0; i < config.Octaves; i++)
            {
                float sampleX = (x + seedX) * frequency;
                float sampleY = (y + seedY) * frequency;
                float sampleZ = (z + seedZ) * frequency;

                float noiseValue = noise.snoise(new float3(sampleX, sampleY, sampleZ));
                sum += noiseValue * amplitude;
                maxAmplitude += amplitude;

                amplitude *= config.Persistence;
                frequency *= config.Lacunarity;
            }

            return (sum / maxAmplitude) * config.HeightScale;
        }

        /// <summary>
        /// 2D fractal noise using noise.cnoise (Perlin) instead of noise.snoise (Simplex).
        /// 24-42% faster than snoise. Used for continentalness and erosion climate layers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sample2DCnoise(float x, float z, NativeNoiseConfig config, long seed)
        {
            float amplitude = 1.0f;
            float frequency = config.Frequency;
            float sum = 0.0f;
            float maxAmplitude = 0.0f;

            float seedX = (seed & 0xFFFF) * 0.3183099f + config.SeedOffset;
            float seedZ = ((seed >> 16) & 0xFFFF) * 0.3183099f + config.SeedOffset;

            for (int i = 0; i < config.Octaves; i++)
            {
                float sampleX = (x + seedX) * frequency;
                float sampleZ = (z + seedZ) * frequency;

                float noiseValue = noise.cnoise(new float2(sampleX, sampleZ));
                sum += noiseValue * amplitude;
                maxAmplitude += amplitude;

                amplitude *= config.Persistence;
                frequency *= config.Lacunarity;
            }

            return (sum / maxAmplitude) * config.HeightScale;
        }
    }
}
