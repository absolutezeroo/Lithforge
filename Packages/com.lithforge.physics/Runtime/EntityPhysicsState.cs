using Unity.Mathematics;

namespace Lithforge.Physics
{
    /// <summary>
    /// Per-entity physics state for batched collision resolution.
    /// Input/output struct: position and velocity are written by the caller,
    /// then modified in-place by <see cref="VoxelColliderJob"/>.
    /// </summary>
    public struct EntityPhysicsState
    {
        public float3 Position;
        public float3 Velocity;
        public float HalfWidth;
        public float Height;
        public CollisionResult Result;
    }
}
