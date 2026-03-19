using System;
using Lithforge.WorldGen.River;
using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    /// <summary>
    /// Serializable river noise configuration that maps 1:1 to NativeRiverConfig.
    /// Used as a nested field in WorldGenSettings for domain-warped river generation.
    /// </summary>
    [Serializable]
    public struct RiverNoiseConfig
    {
        /// <summary>Base frequency of the river noise field.</summary>
        [Min(0.0001f)]
        [Tooltip("Base frequency of the river noise field")]
        public float frequency;

        /// <summary>Frequency of the domain warp noise (typically 2x base frequency).</summary>
        [Min(0.0001f)]
        [Tooltip("Frequency of the domain warp noise (typically 2x base frequency)")]
        public float warpFrequency;

        /// <summary>Displacement strength of domain warping in blocks.</summary>
        [Min(0f)]
        [Tooltip("Displacement strength of domain warping in blocks")]
        public float warpStrength;

        /// <summary>Base threshold for river detection (|noise| less than threshold = river).</summary>
        [Min(0.001f)]
        [Tooltip("Base threshold for river detection (|noise| < threshold = river)")]
        public float baseThreshold;

        /// <summary>Seed offset for the river noise layer.</summary>
        [Tooltip("Seed offset for the river noise layer")]
        public int seedOffset;

        /// <summary>Continentalness below which rivers are suppressed (ocean zone).</summary>
        [Range(0f, 1f)]
        [Tooltip("Continentalness below which rivers are suppressed (ocean zone)")]
        public float oceanContinentalnessCutoff;

        /// <summary>Maximum carve depth in blocks for plains rivers.</summary>
        [Min(1f)]
        [Tooltip("Maximum carve depth in blocks for plains rivers")]
        public float maxCarveDepthPlains;

        /// <summary>Maximum carve depth in blocks for mountain gorges.</summary>
        [Min(1f)]
        [Tooltip("Maximum carve depth in blocks for mountain gorges")]
        public float maxCarveDepthMountain;

        /// <summary>
        /// Converts this managed config to a Burst-compatible NativeRiverConfig.
        /// </summary>
        public NativeRiverConfig ToNativeConfig()
        {
            return new NativeRiverConfig
            {
                Frequency = frequency,
                WarpFrequency = warpFrequency,
                WarpStrength = warpStrength,
                BaseThreshold = baseThreshold,
                SeedOffset = seedOffset,
                OceanContinentalnessCutoff = oceanContinentalnessCutoff,
                MaxCarveDepthPlains = maxCarveDepthPlains,
                MaxCarveDepthMountain = maxCarveDepthMountain,
            };
        }
    }
}
