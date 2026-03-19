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
        /// <summary>Chunk manager providing chunk state queries and block data access.</summary>
        private readonly ChunkManager _chunkManager;

        /// <summary>Burst-accessible state registry for block collision lookups during spawn Y search.</summary>
        private readonly NativeStateRegistry _nativeStateRegistry;

        /// <summary>Creates a new server chunk provider backed by the given chunk manager.</summary>
        public ServerChunkProvider(ChunkManager chunkManager, NativeStateRegistry nativeStateRegistry)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
        }

        /// <summary>Returns true if the chunk at the given coordinate is in the Ready state.</summary>
        public bool IsChunkReady(int3 coord)
        {
            ManagedChunk chunk = _chunkManager.GetChunk(coord);

            return chunk is
            {
                State: ChunkState.Ready,
            };
        }

        /// <summary>Serializes the chunk at the given coordinate for network transmission, returning null if unavailable.</summary>
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

        /// <summary>Searches for a safe Y coordinate to spawn a player at the given XZ position.</summary>
        public int FindSafeSpawnY(int worldX, int worldZ, int chunkYMin, int chunkYMax, int fallbackY)
        {
            return SpawnUtility.FindSafeSpawnY(
                _chunkManager.GetBlock, _nativeStateRegistry,
                worldX, worldZ, chunkYMin, chunkYMax, fallbackY);
        }

        /// <summary>Returns true if the chunk at the given coordinate has completed generation.</summary>
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

        /// <summary>Returns true if the chunk at the given coordinate is generated and contains only air.</summary>
        public bool IsChunkAllAir(int3 coord)
        {
            ManagedChunk chunk = _chunkManager.GetChunk(coord);

            return chunk is
            {
                State: >= ChunkState.Generated,
                IsAllAir: true,
            };
        }

        /// <summary>Returns the network version counter for the given chunk, or -1 if not loaded.</summary>
        public int GetChunkNetworkVersion(int3 coord)
        {
            ManagedChunk chunk = _chunkManager.GetChunk(coord);

            return chunk?.NetworkVersion ?? -1;
        }
    }
}
