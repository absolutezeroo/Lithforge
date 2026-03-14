using System.Runtime.InteropServices;

namespace Lithforge.WorldGen.River
{
    /// <summary>
    /// Blittable configuration for domain-warped river noise generation.
    /// Used by RiverNoiseJob in Burst-compiled code.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeRiverConfig
    {
        /// <summary>Base frequency of the river noise field.</summary>
        public float Frequency;

        /// <summary>Frequency of the domain warp noise (typically 2x base frequency).</summary>
        public float WarpFrequency;

        /// <summary>Displacement strength of domain warping in blocks.</summary>
        public float WarpStrength;

        /// <summary>Base threshold for river detection (|noise| less than this = river).</summary>
        public float BaseThreshold;

        /// <summary>Seed offset for the river noise layer.</summary>
        public int SeedOffset;

        /// <summary>Continentalness below which rivers are suppressed (ocean zone).</summary>
        public float OceanContinentalnessCutoff;

        /// <summary>Maximum carve depth in blocks for plains rivers.</summary>
        public float MaxCarveDepthPlains;

        /// <summary>Maximum carve depth in blocks for mountain gorges.</summary>
        public float MaxCarveDepthMountain;
    }
}
