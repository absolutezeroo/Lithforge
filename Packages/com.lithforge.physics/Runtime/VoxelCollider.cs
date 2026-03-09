using System;
using Unity.Mathematics;

namespace Lithforge.Physics
{
    /// <summary>
    /// AABB vs voxel grid collision resolution.
    /// Uses swept axis-independent resolution (resolve Y first for ground detection,
    /// then X, then Z) to prevent corner-clipping artifacts.
    ///
    /// Called on the main thread only — uses managed delegates for block access.
    /// </summary>
    public static class VoxelCollider
    {
        /// <summary>
        /// Resolves collision between a moving entity AABB and the voxel grid.
        /// Modifies position and velocity in-place, zeroing velocity components
        /// on axes where collisions occur.
        /// </summary>
        /// <param name="position">Entity position (feet position). Modified in-place.</param>
        /// <param name="velocity">Entity velocity (blocks/frame-delta). Modified in-place.</param>
        /// <param name="halfWidth">Half-width of the entity hitbox on X and Z.</param>
        /// <param name="height">Total height of the entity hitbox.</param>
        /// <param name="isSolid">Predicate returning true if the block at the given coord is solid.</param>
        /// <returns>Collision result indicating ground, ceiling, and wall contact.</returns>
        public static CollisionResult Resolve(
            ref float3 position,
            ref float3 velocity,
            float halfWidth,
            float height,
            Func<int3, bool> isSolid)
        {
            CollisionResult result = new CollisionResult();

            // Build the entity AABB from feet position
            Aabb entityBox = BuildEntityBox(position, halfWidth, height);

            // Broad phase: expand AABB by velocity to find all potentially colliding blocks
            Aabb broadPhase = entityBox.ExpandByVelocity(velocity);
            broadPhase = broadPhase.Expand(new float3(0.01f));

            int minX = (int)math.floor(broadPhase.Min.x);
            int minY = (int)math.floor(broadPhase.Min.y);
            int minZ = (int)math.floor(broadPhase.Min.z);
            int maxX = (int)math.floor(broadPhase.Max.x);
            int maxY = (int)math.floor(broadPhase.Max.y);
            int maxZ = (int)math.floor(broadPhase.Max.z);

            // Resolve Y axis first (most important for ground detection)
            position.y += velocity.y;
            entityBox = BuildEntityBox(position, halfWidth, height);

            for (int bx = minX; bx <= maxX; bx++)
            {
                for (int by = minY; by <= maxY; by++)
                {
                    for (int bz = minZ; bz <= maxZ; bz++)
                    {
                        int3 blockCoord = new int3(bx, by, bz);

                        if (!isSolid(blockCoord))
                        {
                            continue;
                        }

                        Aabb blockBox = Aabb.FromBlockCoord(blockCoord);

                        if (!entityBox.Intersects(blockBox))
                        {
                            continue;
                        }

                        if (velocity.y < 0f)
                        {
                            // Moving down — push up to top of block
                            float correction = blockBox.Max.y - entityBox.Min.y;
                            position.y += correction;
                            result.OnGround = true;
                        }
                        else if (velocity.y > 0f)
                        {
                            // Moving up — push down to bottom of block
                            float correction = blockBox.Min.y - entityBox.Max.y;
                            position.y += correction;
                            result.HitCeiling = true;
                        }

                        velocity.y = 0f;
                        entityBox = BuildEntityBox(position, halfWidth, height);
                    }
                }
            }

            // Resolve X axis
            position.x += velocity.x;
            entityBox = BuildEntityBox(position, halfWidth, height);

            for (int bx = minX; bx <= maxX; bx++)
            {
                for (int by = minY; by <= maxY; by++)
                {
                    for (int bz = minZ; bz <= maxZ; bz++)
                    {
                        int3 blockCoord = new int3(bx, by, bz);

                        if (!isSolid(blockCoord))
                        {
                            continue;
                        }

                        Aabb blockBox = Aabb.FromBlockCoord(blockCoord);

                        if (!entityBox.Intersects(blockBox))
                        {
                            continue;
                        }

                        if (velocity.x > 0f)
                        {
                            float correction = blockBox.Min.x - entityBox.Max.x;
                            position.x += correction;
                        }
                        else if (velocity.x < 0f)
                        {
                            float correction = blockBox.Max.x - entityBox.Min.x;
                            position.x += correction;
                        }

                        velocity.x = 0f;
                        result.HitWall = true;
                        entityBox = BuildEntityBox(position, halfWidth, height);
                    }
                }
            }

            // Resolve Z axis
            position.z += velocity.z;
            entityBox = BuildEntityBox(position, halfWidth, height);

            for (int bx = minX; bx <= maxX; bx++)
            {
                for (int by = minY; by <= maxY; by++)
                {
                    for (int bz = minZ; bz <= maxZ; bz++)
                    {
                        int3 blockCoord = new int3(bx, by, bz);

                        if (!isSolid(blockCoord))
                        {
                            continue;
                        }

                        Aabb blockBox = Aabb.FromBlockCoord(blockCoord);

                        if (!entityBox.Intersects(blockBox))
                        {
                            continue;
                        }

                        if (velocity.z > 0f)
                        {
                            float correction = blockBox.Min.z - entityBox.Max.z;
                            position.z += correction;
                        }
                        else if (velocity.z < 0f)
                        {
                            float correction = blockBox.Max.z - entityBox.Min.z;
                            position.z += correction;
                        }

                        velocity.z = 0f;
                        result.HitWall = true;
                        entityBox = BuildEntityBox(position, halfWidth, height);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Builds an entity AABB from feet position, half-width, and height.
        /// The position represents the bottom-center of the entity.
        /// </summary>
        private static Aabb BuildEntityBox(float3 feetPosition, float halfWidth, float height)
        {
            return new Aabb(
                new float3(feetPosition.x - halfWidth, feetPosition.y, feetPosition.z - halfWidth),
                new float3(feetPosition.x + halfWidth, feetPosition.y + height, feetPosition.z + halfWidth));
        }
    }
}
