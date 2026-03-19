using System;
using Lithforge.WorldGen.Noise;
using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    /// <summary>
    /// Serializable noise layer configuration that maps 1:1 to NativeNoiseConfig.
    /// Used as a nested field in Settings SOs to avoid repeating the same six fields
    /// for every noise layer (terrain, temperature, humidity, cave).
    /// </summary>
    [Serializable]
    public struct NoiseLayerConfig
    {
        /// <summary>Base frequency of the noise layer.</summary>
        [Min(0.0001f)]
        [Tooltip("Base frequency of the noise layer")]
        public float frequency;

        /// <summary>Octave frequency multiplier (each octave's frequency = previous * lacunarity).</summary>
        [Tooltip("Octave frequency multiplier")]
        public float lacunarity;

        /// <summary>Octave amplitude decay factor (each octave's amplitude = previous * persistence).</summary>
        [Range(0f, 1f)]
        [Tooltip("Octave amplitude decay")]
        public float persistence;

        /// <summary>Vertical scale applied to the final noise output.</summary>
        [Tooltip("Vertical scale applied to noise output")]
        public float heightScale;

        /// <summary>Number of fractal noise octaves summed together.</summary>
        [Range(1, 8)]
        [Tooltip("Number of noise octaves")]
        public int octaves;

        /// <summary>Seed offset added to the world seed for this noise layer.</summary>
        [Tooltip("Seed offset for this noise layer")]
        public int seedOffset;

        /// <summary>
        /// Converts this managed config to a Burst-compatible NativeNoiseConfig.
        /// </summary>
        public NativeNoiseConfig ToNativeConfig()
        {
            return new NativeNoiseConfig
            {
                Frequency = frequency,
                Lacunarity = lacunarity,
                Persistence = persistence,
                HeightScale = heightScale,
                Octaves = octaves,
                SeedOffset = seedOffset,
            };
        }
    }
}
