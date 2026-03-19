using System;

using Lithforge.Physics;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Runtime.Input
{
    /// <summary>
    ///     Shared utility for checking whether a voxel at a world coordinate is solid.
    ///     Used by both PlayerController and BlockInteraction.
    ///     Provides both a managed delegate (for VoxelRaycast) and a Burst-compatible
    ///     <see cref="SolidBlockQuery" /> struct (for VoxelCollider).
    ///     Supports an optional collision state override for unconfirmed block predictions:
    ///     when a block has been optimistically placed or broken but not yet acknowledged,
    ///     collision resolves against the pre-prediction state to prevent movement errors.
    /// </summary>
    internal static class SolidBlockHelper
    {
        /// <summary>Checks if the block at the given world coordinate is solid, treating unloaded chunks as solid.</summary>
        public static bool IsSolid(
            int3 worldCoord,
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry)
        {
            return IsSolid(worldCoord, chunkManager, nativeStateRegistry, null);
        }

        /// <summary>
        ///     Checks if the block at the given world coordinate is solid, with an optional
        ///     collision state override. When <paramref name="collisionOverride" /> provides a
        ///     state for this coordinate, that state is used for collision instead of the current
        ///     block state. This prevents unconfirmed predictions from affecting movement.
        /// </summary>
        public static bool IsSolid(
            int3 worldCoord,
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            Func<int3, StateId?> collisionOverride)
        {
            // Treat unloaded/non-ready chunks as solid walls.
            // This prevents the player from walking or falling into unloaded terrain.
            if (!chunkManager.IsBlockLoaded(worldCoord))
            {
                return true;
            }

            StateId stateId;

            if (collisionOverride is not null)
            {
                StateId? overrideState = collisionOverride(worldCoord);

                stateId = overrideState ?? chunkManager.GetBlock(worldCoord);
            }
            else
            {
                stateId = chunkManager.GetBlock(worldCoord);
            }

            BlockStateCompact compact = nativeStateRegistry.States[stateId.Value];

            return compact.CollisionShape != 0;
        }

        /// <summary>
        ///     Creates a cached delegate for the IsSolid check. Call once during initialization
        ///     and store the result to avoid per-frame delegate allocation.
        /// </summary>
        public static Func<int3, bool> CreateDelegate(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry)
        {
            return coord => IsSolid(coord, chunkManager, nativeStateRegistry);
        }

        /// <summary>
        ///     Builds a Burst-compatible <see cref="SolidBlockQuery" /> pre-filled with solidity
        ///     data for the broad-phase region of a collision resolve call.
        ///     The returned SolidMap uses Allocator.Temp and must be disposed by the caller.
        /// </summary>
        public static SolidBlockQuery Build(
            float3 position,
            float3 velocity,
            float halfWidth,
            float height,
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry)
        {
            return Build(position, velocity, halfWidth, height,
                chunkManager, nativeStateRegistry, null);
        }

        /// <summary>
        ///     Builds a Burst-compatible <see cref="SolidBlockQuery" /> with an optional
        ///     collision state override for unconfirmed block predictions.
        ///     The returned SolidMap uses Allocator.Temp and must be disposed by the caller.
        /// </summary>
        public static SolidBlockQuery Build(
            float3 position,
            float3 velocity,
            float halfWidth,
            float height,
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            Func<int3, StateId?> collisionOverride)
        {
            VoxelCollider.ComputeBroadPhaseBounds(
                position, velocity, halfWidth, height,
                out int3 bpMin, out int3 bpMax);

            int sizeX = bpMax.x - bpMin.x + 1;
            int sizeY = bpMax.y - bpMin.y + 1;
            int sizeZ = bpMax.z - bpMin.z + 1;
            int volume = sizeX * sizeY * sizeZ;

            NativeHashMap<int3, bool> solidMap = new(volume, Allocator.Temp);

            for (int x = bpMin.x; x <= bpMax.x; x++)
            {
                for (int y = bpMin.y; y <= bpMax.y; y++)
                {
                    for (int z = bpMin.z; z <= bpMax.z; z++)
                    {
                        int3 coord = new(x, y, z);
                        solidMap.TryAdd(coord,
                            IsSolid(coord, chunkManager, nativeStateRegistry, collisionOverride));
                    }
                }
            }

            return new SolidBlockQuery
            {
                SolidMap = solidMap,
            };
        }
    }
}
