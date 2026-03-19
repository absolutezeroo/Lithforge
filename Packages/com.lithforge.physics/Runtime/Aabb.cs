using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Lithforge.Physics
{
    /// <summary>
    /// Axis-aligned bounding box. Blittable struct.
    /// </summary>
    public struct Aabb
    {
        /// <summary>Lower corner of the bounding box.</summary>
        public float3 Min;

        /// <summary>Upper corner of the bounding box.</summary>
        public float3 Max;

        /// <summary>Creates an AABB from explicit min and max corners.</summary>
        public Aabb(float3 min, float3 max)
        {
            Min = min;
            Max = max;
        }

        /// <summary>Midpoint between Min and Max.</summary>
        public float3 Center
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (Min + Max) * 0.5f;
            }
        }

        /// <summary>Width, height, and depth of the bounding box.</summary>
        public float3 Size
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Max - Min;
            }
        }

        /// <summary>Returns true if this AABB overlaps with another on all 3 axes.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Intersects(Aabb other)
        {
            return Min.x < other.Max.x && Max.x > other.Min.x
                && Min.y < other.Max.y && Max.y > other.Min.y
                && Min.z < other.Max.z && Max.z > other.Min.z;
        }

        /// <summary>Returns true if the given point lies within the AABB (inclusive).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(float3 point)
        {
            return point.x >= Min.x && point.x <= Max.x
                && point.y >= Min.y && point.y <= Max.y
                && point.z >= Min.z && point.z <= Max.z;
        }

        /// <summary>
        /// Returns a new AABB expanded by the given amount on each side.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Aabb Expand(float3 amount)
        {
            return new Aabb(Min - amount, Max + amount);
        }

        /// <summary>
        /// Returns a new AABB expanded to include the given velocity vector.
        /// Used for broad-phase swept collision.
        /// </summary>
        public Aabb ExpandByVelocity(float3 velocity)
        {
            float3 newMin = Min;
            float3 newMax = Max;

            if (velocity.x < 0)
            {
                newMin.x += velocity.x;
            }
            else
            {
                newMax.x += velocity.x;
            }

            if (velocity.y < 0)
            {
                newMin.y += velocity.y;
            }
            else
            {
                newMax.y += velocity.y;
            }

            if (velocity.z < 0)
            {
                newMin.z += velocity.z;
            }
            else
            {
                newMax.z += velocity.z;
            }

            return new Aabb(newMin, newMax);
        }

        /// <summary>
        /// Creates an AABB for a full voxel cube at the given integer coordinates.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Aabb FromBlockCoord(int3 coord)
        {
            return new Aabb(new float3(coord), new float3(coord) + new float3(1f));
        }
    }
}
