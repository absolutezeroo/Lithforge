using Unity.Mathematics;

namespace Lithforge.WorldGen.Lighting
{
    /// <summary>
    /// Blittable struct for border light entries produced by light jobs.
    /// Stored in a NativeList and read back on the main thread.
    /// </summary>
    public struct NativeBorderLightEntry
    {
        public int3 LocalPosition;
        public byte PackedLight;
        public byte Face;
    }
}
