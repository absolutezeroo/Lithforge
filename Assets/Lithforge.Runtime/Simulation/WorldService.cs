using System.Collections.Generic;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Mathematics;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    /// Singleplayer implementation of <see cref="IWorldService"/>.
    /// Delegates directly to <see cref="ChunkManager"/>.
    /// </summary>
    public sealed class WorldService : IWorldService
    {
        /// <summary>Backing chunk manager that provides block read/write access.</summary>
        private readonly ChunkManager _chunkManager;

        /// <summary>Creates a new world service backed by the given chunk manager.</summary>
        public WorldService(ChunkManager chunkManager)
        {
            _chunkManager = chunkManager;
        }

        /// <summary>Gets the block state at the given world coordinate, returning Air if the chunk is not loaded.</summary>
        public StateId GetBlock(int3 worldCoord)
        {
            return _chunkManager.GetBlock(worldCoord);
        }

        /// <summary>Sets the block at the given world coordinate and appends dirtied chunk coords.</summary>
        public void SetBlock(int3 worldCoord, StateId state, List<int3> dirtiedChunks)
        {
            _chunkManager.SetBlock(worldCoord, state, dirtiedChunks);
        }

        /// <summary>Returns true if the block at the given coordinate is in a loaded, generated chunk.</summary>
        public bool IsBlockLoaded(int3 worldCoord)
        {
            return _chunkManager.IsBlockLoaded(worldCoord);
        }
    }
}
