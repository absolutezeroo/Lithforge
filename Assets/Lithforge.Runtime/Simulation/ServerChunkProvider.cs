using Lithforge.Network.Chunk;
using Lithforge.Network.Server;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Spawn;

using Unity.Mathematics;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    ///     Tier 3 implementation of <see cref="IServerChunkProvider" />. Bridges the network
    ///     package's <see cref="ServerGameLoop" /> to the runtime's <see cref="ChunkManager" />
    ///     for chunk readiness queries and serialization.
    /// </summary>
    public sealed class ServerChunkProvider : IServerChunkProvider
    {
        private readonly ChunkManager _chunkManager;

        private readonly NativeStateRegistry _nativeStateRegistry;

        public ServerChunkProvider(ChunkManager chunkManager, NativeStateRegistry nativeStateRegistry)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
        }

        public bool IsChunkReady(int3 coord)
        {
            ManagedChunk chunk = _chunkManager.GetChunk(coord);

            return chunk is
            {
                State: ChunkState.Ready,
            };
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

            // Complete any in-flight light job before reading LightData to avoid
            // the job safety system rejecting the read.
            if (chunk.LightJobInFlight)
            {
                chunk.ActiveJobHandle.Complete();
            }

            return ChunkNetSerializer.SerializeFullChunk(chunk.Data, chunk.LightData);
        }

        public int FindSafeSpawnY(int worldX, int worldZ, int chunkYMin, int chunkYMax, int fallbackY)
        {
            return SpawnUtility.FindSafeSpawnY(
                _chunkManager.GetBlock, _nativeStateRegistry,
                worldX, worldZ, chunkYMin, chunkYMax, fallbackY);
        }

        public bool IsChunkGenerated(int3 coord)
        {
            ManagedChunk chunk = _chunkManager.GetChunk(coord);

            return chunk is
            {
                State: >= ChunkState.Generated,
                Data:
                {
                    IsCreated: true,
                },
            };
        }

        public bool IsChunkAllAir(int3 coord)
        {
            ManagedChunk chunk = _chunkManager.GetChunk(coord);

            return chunk is
            {
                State: >= ChunkState.Generated,
                IsAllAir: true,
            };
        }
    }
}
