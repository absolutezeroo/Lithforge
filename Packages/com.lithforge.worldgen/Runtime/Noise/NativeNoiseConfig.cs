using System.Runtime.InteropServices;

namespace Lithforge.WorldGen.Noise
{
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeNoiseConfig
    {
        public float Frequency;
        public float Lacunarity;
        public float Persistence;
        public float HeightScale;
        public int Octaves;
        public float SeedOffset;
    }
}
