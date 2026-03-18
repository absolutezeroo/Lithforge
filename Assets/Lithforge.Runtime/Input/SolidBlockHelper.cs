using System;
using Lithforge.Physics;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Runtime.Input
{
    /// <summary>
    /// Shared utility for checking whether a voxel at a world coordinate is solid.
    /// Used by both PlayerController and BlockInteraction.
    /// Provides both a managed delegate (for VoxelRaycast) and a Burst-compatible
    /// <see cref="SolidBlockQuery"/> struct (for VoxelCollider).
    /// </summary>
    internal static class SolidBlockHelper
    {
        public static bool IsSolid(
            int3 worldCoord,
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry)
        {
            // Treat unloaded/non-ready chunks as solid walls.
            // This prevents the player from walking or falling into unloaded terrain.
            if (!chunkManager.IsBlockLoaded(worldCoord))
            {
                return true;
            }

            StateId stateId = chunkManager.GetBlock(worldCoord);
            BlockStateCompact compact = nativeStateRegistry.States[stateId.Value];

            return compact.CollisionShape != 0;
        }

        /// <summary>
        /// Creates a cached delegate for the IsSolid check. Call once during initialization
        /// and store the result to avoid per-frame delegate allocation.
        /// </summary>
        public static Func<int3, bool> CreateDelegate(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry)
        {
            return (int3 coord) => IsSolid(coord, chunkManager, nativeStateRegistry);
        }

        /// <summary>
        /// Builds a Burst-compatible <see cref="SolidBlockQuery"/> pre-filled with solidity
        /// data for the broad-phase region of a collision resolve call.
        /// The returned SolidMap uses Allocator.Temp and must be disposed by the caller.
        /// </summary>
        public static SolidBlockQuery Build(
            float3 position,
            float3 velocity,
            float halfWidth,
            float height,
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry)
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
                        solidMap.TryAdd(coord, IsSolid(coord, chunkManager, nativeStateRegistry));
                    }
                }
            }

            return new SolidBlockQuery { SolidMap = solidMap };
        }
    }
}
