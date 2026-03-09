using Unity.Mathematics;

namespace Lithforge.Physics
{
    /// <summary>
    /// Result of a voxel raycast. Blittable struct.
    /// </summary>
    public struct RaycastHit
    {
        /// <summary>
        /// True if the ray hit a solid block within range.
        /// </summary>
        public bool DidHit;

        /// <summary>
        /// World-space position of the hit point on the block face.
        /// </summary>
        public float3 Position;

        /// <summary>
        /// Integer coordinates of the hit block in world space.
        /// </summary>
        public int3 BlockCoord;

        /// <summary>
        /// Face normal of the hit surface (axis-aligned, one of 6 directions).
        /// Points away from the hit block toward the ray origin.
        /// </summary>
        public int3 Normal;

        /// <summary>
        /// Distance from the ray origin to the hit point.
        /// </summary>
        public float Distance;
    }
}
