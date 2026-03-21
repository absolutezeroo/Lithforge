using System;

using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using Unity.Mathematics;

namespace Lithforge.Voxel.Spawn
{
    /// <summary>
    ///     Shared spawn-position utilities used by both the singleplayer SpawnManager
    ///     and the server-side ServerGameLoop. Lives in com.lithforge.voxel so all
    ///     tiers can access it.
    /// </summary>
    public static class SpawnUtility
    {
        /// <summary>
        ///     Scans downward from the top of the specified chunk Y-range at the given
        ///     world XZ position. Returns the Y coordinate of the first air block
        ///     directly above a solid block (CollisionShape != 0).
        ///     Returns <paramref name="fallbackY" /> if no solid block is found.
        /// </summary>
        /// <param name="getBlock">Block accessor: given a world coordinate, returns the StateId.</param>
        /// <param name="states">Native state registry for collision shape lookup.</param>
        /// <param name="worldX">World X coordinate to scan.</param>
        /// <param name="worldZ">World Z coordinate to scan.</param>
        /// <param name="chunkYMin">Minimum chunk Y coordinate of the scan range.</param>
        /// <param name="chunkYMax">Maximum chunk Y coordinate of the scan range.</param>
        /// <param name="fallbackY">Y to return if no solid ground found.</param>
        public static int FindSafeSpawnY(
            Func<int3, StateId> getBlock,
            NativeStateRegistry states,
            int worldX, int worldZ,
            int chunkYMin, int chunkYMax,
            int fallbackY)
        {
            int maxBlockY = (chunkYMax + 1) * ChunkConstants.Size - 1;
            int minBlockY = chunkYMin * ChunkConstants.Size;

            for (int y = maxBlockY; y >= minBlockY; y--)
            {
                int3 coord = new(worldX, y, worldZ);
                StateId stateId = getBlock(coord);
                BlockStateCompact state = states.States[stateId.Value];

                if (state.CollisionShape != 0)
                {
                    return y + 1;
                }
            }

            return fallbackY;
        }

        /// <summary>
        ///     Checks whether a position is safe for a player (feet + head blocks are non-solid).
        /// </summary>
        public static bool IsPositionSafe(
            Func<int3, StateId> getBlock,
            NativeStateRegistry states,
            int3 feetBlock)
        {
            int3 headBlock = new(feetBlock.x, feetBlock.y + 1, feetBlock.z);

            StateId feetState = getBlock(feetBlock);
            StateId headState = getBlock(headBlock);

            BlockStateCompact feetCompact = states.States[feetState.Value];
            BlockStateCompact headCompact = states.States[headState.Value];

            return feetCompact.CollisionShape == 0 && headCompact.CollisionShape == 0;
        }
    }
}
