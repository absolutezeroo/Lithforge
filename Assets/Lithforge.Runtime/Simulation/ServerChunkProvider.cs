using Lithforge.Network.Server;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Network;
using Unity.Mathematics;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    /// Tier 3 implementation of <see cref="IServerChunkProvider"/>. Bridges the network
    /// package's <see cref="ServerGameLoop"/> to the runtime's <see cref="ChunkManager"/>
    /// for chunk readiness queries and serialization.
    /// </summary>
    public sealed class ServerChunkProvider : IServerChunkProvider
    {
        private readonly ChunkManager _chunkManager;

        public ServerChunkProvider(ChunkManager chunkManager)
        {
            _chunkManager = chunkManager;
        }

        public bool IsChunkReady(int3 coord)
        {
            ManagedChunk chunk = _chunkManager.GetChunk(coord);
            return chunk != null && chunk.State == ChunkState.Ready;
        }

        public byte[] SerializeChunk(int3 coord)
        {
            ManagedChunk chunk = _chunkManager.GetChunk(coord);

            if (chunk == null || chunk.State < ChunkState.Generated)
            {
                return null;
            }

            if (!chunk.Data.IsCreated)
            {
                return null;
            }

            return ChunkNetSerializer.SerializeFullChunk(chunk.Data, chunk.LightData);
        }

        public bool AreChunksReady(int3 center, int radius, int yMin, int yMax)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    for (int y = yMin; y <= yMax; y++)
                    {
                        int3 coord = new(center.x + x, y, center.z + z);

                        if (!IsChunkReady(coord))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}
