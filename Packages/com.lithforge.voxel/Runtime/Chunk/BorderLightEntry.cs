using Unity.Mathematics;

namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    /// Blittable struct representing a light value at a chunk border position.
    /// Used to propagate light across chunk boundaries.
    /// </summary>
    public struct BorderLightEntry
    {
        /// <summary>Local position within the chunk (0-31 on each axis).</summary>
        public int3 LocalPosition;

        /// <summary>Packed light value (high nibble = sun, low nibble = block).</summary>
        public byte PackedLight;

        /// <summary>Face index (0=+X, 1=-X, 2=+Y, 3=-Y, 4=+Z, 5=-Z) indicating which border.</summary>
        public byte Face;
    }
}
