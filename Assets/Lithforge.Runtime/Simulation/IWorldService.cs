using System.Collections.Generic;
using Lithforge.Voxel.Block;
using Unity.Mathematics;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    /// Read-write facade over the world's block state.
    /// Hides the <see cref="Lithforge.Voxel.Chunk.ChunkManager"/> implementation
    /// so that simulation services can be tested against a mock world.
    /// In singleplayer, <see cref="WorldService"/> delegates directly to ChunkManager.
    /// </summary>
    public interface IWorldService
    {
        /// <summary>
        /// Gets the block state at a world coordinate.
        /// Returns <see cref="StateId.Air"/> if the chunk is not loaded.
        /// </summary>
        public StateId GetBlock(int3 worldCoord);

        /// <summary>
        /// Sets the block state at a world coordinate. Marks the chunk and
        /// border-adjacent neighbors for relighting and remeshing.
        /// Appends dirtied chunk coordinates to <paramref name="dirtiedChunks"/>.
        /// </summary>
        public void SetBlock(int3 worldCoord, StateId state, List<int3> dirtiedChunks);

        /// <summary>
        /// Returns true if the block at the given coordinate is in a loaded,
        /// sufficiently-generated chunk.
        /// </summary>
        public bool IsBlockLoaded(int3 worldCoord);
    }
}
