using System;
using System.Runtime.CompilerServices;

using Unity.Mathematics;

namespace Lithforge.Physics
{
    /// <summary>
    ///     DDA (Digital Differential Analyzer) voxel raycast.
    ///     Traverses voxels along a ray and returns the first solid block hit.
    ///     Called on the main thread only — uses managed delegates for block access.
    ///     Typically completes in 5-15 DDA steps at interaction range (5 blocks).
    /// </summary>
    public static class VoxelRaycast
    {
        /// <summary>
        ///     Casts a ray through the voxel grid and returns the first solid block hit.
        /// </summary>
        /// <param name="origin">Ray origin in world space.</param>
        /// <param name="direction">Ray direction (does not need to be normalized).</param>
        /// <param name="maxDistance">Maximum ray distance in blocks.</param>
        /// <param name="isSolid">Predicate returning true if the block at the given world coord is solid.</param>
        /// <returns>RaycastHit with DidHit=true if a solid block was found, default otherwise.</returns>
        public static RaycastHit Cast(
            float3 origin,
            float3 direction,
            float maxDistance,
            Func<int3, bool> isSolid)
        {
            float3 dir = math.normalizesafe(direction);

            // Zero direction → no hit
            if (math.lengthsq(dir) < 1e-12f)
            {
                return default;
            }

            // Step direction per axis: +1 or -1
            int3 step = new(
                dir.x >= 0f ? 1 : -1,
                dir.y >= 0f ? 1 : -1,
                dir.z >= 0f ? 1 : -1);

            // Current voxel coordinates
            int3 blockCoord = new(
                (int)math.floor(origin.x),
                (int)math.floor(origin.y),
                (int)math.floor(origin.z));

            // tMax: parametric distance to the next voxel boundary on each axis
            float3 tMax = new(
                ComputeInitialT(origin.x, dir.x, step.x),
                ComputeInitialT(origin.y, dir.y, step.y),
                ComputeInitialT(origin.z, dir.z, step.z));

            // tDelta: parametric distance to cross one full voxel on each axis
            float3 tDelta = new(
                math.abs(dir.x) > 1e-6f ? math.abs(1.0f / dir.x) : float.MaxValue,
                math.abs(dir.y) > 1e-6f ? math.abs(1.0f / dir.y) : float.MaxValue,
                math.abs(dir.z) > 1e-6f ? math.abs(1.0f / dir.z) : float.MaxValue);

            // Track which face was crossed to determine the hit normal
            int3 normal = int3.zero;
            float dist = 0f;

            // Check the starting block (player might be inside a solid block)
            if (isSolid(blockCoord))
            {
                return new RaycastHit
                {
                    DidHit = true,
                    BlockCoord = blockCoord,
                    Normal = int3.zero,
                    Distance = 0f,
                    Position = origin,
                };
            }

            // DDA traversal loop
            int maxSteps = (int)(maxDistance * 2f) + 4;

            for (int i = 0; i < maxSteps; i++)
            {
                // Advance along the axis with the smallest tMax
                if (tMax.x < tMax.y && tMax.x < tMax.z)
                {
                    dist = tMax.x;
                    if (dist > maxDistance) { break; }
                    blockCoord.x += step.x;
                    tMax.x += tDelta.x;
                    normal = new int3(-step.x, 0, 0);
                }
                else if (tMax.y < tMax.z)
                {
                    dist = tMax.y;
                    if (dist > maxDistance) { break; }
                    blockCoord.y += step.y;
                    tMax.y += tDelta.y;
                    normal = new int3(0, -step.y, 0);
                }
                else
                {
                    dist = tMax.z;
                    if (dist > maxDistance) { break; }
                    blockCoord.z += step.z;
                    tMax.z += tDelta.z;
                    normal = new int3(0, 0, -step.z);
                }

                if (isSolid(blockCoord))
                {
                    return new RaycastHit
                    {
                        DidHit = true,
                        BlockCoord = blockCoord,
                        Normal = normal,
                        Distance = dist,
                        Position = origin + dir * dist,
                    };
                }
            }

            return default;
        }

        /// <summary>
        ///     Computes the parametric distance from position to the next voxel boundary
        ///     along a single axis.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ComputeInitialT(float pos, float dir, int step)
        {
            if (math.abs(dir) < 1e-6f)
            {
                return float.MaxValue;
            }

            float boundary;

            if (step > 0)
            {
                boundary = math.floor(pos) + 1f;
            }
            else
            {
                boundary = math.floor(pos);

                // If exactly on boundary, step back one voxel
                if (math.abs(boundary - pos) < 1e-6f)
                {
                    boundary -= 1f;
                }
            }

            return (boundary - pos) / dir;
        }
    }
}
