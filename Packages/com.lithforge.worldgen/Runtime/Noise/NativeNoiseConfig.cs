using System.Runtime.InteropServices;

namespace Lithforge.WorldGen.Noise
{
    /// <summary>
    /// Burst-compatible noise layer parameters, baked from WorldGenSettings at content load.
    /// Consumed by NativeNoise.Sample2D/Sample3D in generation jobs.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeNoiseConfig
    {
        /// <summary>Base sampling frequency. Lower values produce broader features.</summary>
        public float Frequency;

        /// <summary>Frequency multiplier applied per octave (typically ~2.0).</summary>
        public float Lacunarity;

        /// <summary>Amplitude multiplier applied per octave (typically 0.4-0.6). Controls detail falloff.</summary>
        public float Persistence;

        /// <summary>Output amplitude scaling. Converts normalized noise to world-space block units.</summary>
        public float HeightScale;

        /// <summary>Number of fractal octaves to sum (more = finer detail, higher cost).</summary>
        public int Octaves;

        /// <summary>Offset added to the world seed so different noise layers produce uncorrelated patterns.</summary>
        public float SeedOffset;
    }
}
