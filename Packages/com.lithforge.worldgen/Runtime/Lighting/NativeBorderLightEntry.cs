using Unity.Mathematics;

namespace Lithforge.WorldGen.Lighting
{
    /// <summary>
    /// Blittable struct for border light entries produced by light jobs.
    /// Stored in a NativeList and read back on the main thread.
    /// </summary>
    public struct NativeBorderLightEntry
    {
        /// <summary>Chunk-local position of the border voxel that leaked light.</summary>
        public int3 LocalPosition;

        /// <summary>Nibble-packed light value (sun << 4 | block) to propagate into the neighbor.</summary>
        public byte PackedLight;

        /// <summary>Face index (0-5) indicating which chunk face the light crosses.</summary>
        public byte Face;
    }
}
