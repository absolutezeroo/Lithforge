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
        /// <summary>World-space feet position of the entity.</summary>
        public float3 Position;

        /// <summary>Current velocity in blocks per tick-delta.</summary>
        public float3 Velocity;

        /// <summary>Half-width of the entity hitbox on X and Z axes.</summary>
        public float HalfWidth;

        /// <summary>Total height of the entity hitbox.</summary>
        public float Height;

        /// <summary>Collision result populated after VoxelColliderJob completes.</summary>
        public CollisionResult Result;
    }
}
