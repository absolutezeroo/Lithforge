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
        private readonly ChunkManager _chunkManager;

        public WorldService(ChunkManager chunkManager)
        {
            _chunkManager = chunkManager;
        }

        public StateId GetBlock(int3 worldCoord)
        {
            return _chunkManager.GetBlock(worldCoord);
        }

        public void SetBlock(int3 worldCoord, StateId state, List<int3> dirtiedChunks)
        {
            _chunkManager.SetBlock(worldCoord, state, dirtiedChunks);
        }

        public bool IsBlockLoaded(int3 worldCoord)
        {
            return _chunkManager.IsBlockLoaded(worldCoord);
        }
    }
}
