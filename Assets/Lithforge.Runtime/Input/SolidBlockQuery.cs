using System;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Mathematics;

namespace Lithforge.Runtime.Input
{
    /// <summary>
    /// Shared utility for checking whether a voxel at a world coordinate is solid.
    /// Used by both PlayerController and BlockInteraction to avoid code duplication.
    /// </summary>
    internal static class SolidBlockQuery
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
    }
}
