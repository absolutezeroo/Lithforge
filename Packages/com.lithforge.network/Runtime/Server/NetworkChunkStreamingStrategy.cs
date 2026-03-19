using Lithforge.Network.Chunk;
using Lithforge.Network.Connection;
using Lithforge.Network.Messages;

using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Chunk streaming strategy for remote peers: serializes chunk data and sends
    ///     it over the network via the server transport. Maintains a
    ///     <see cref="CompressedChunkCache"/> so that identical chunk data is serialized
    ///     once and reused for every peer that needs it, until a block edit invalidates
    ///     the cached entry.
    /// </summary>
    public sealed class NetworkChunkStreamingStrategy : IChunkStreamingStrategy
    {
        private readonly CompressedChunkCache _cache = new();

        private readonly IServerChunkProvider _chunkProvider;

        private readonly INetworkServer _server;

        public NetworkChunkStreamingStrategy(INetworkServer server, IServerChunkProvider chunkProvider)
        {
            _server = server;
            _chunkProvider = chunkProvider;
        }

        /// <summary>Number of cached compressed chunks currently held.</summary>
        public int CacheCount
        {
            get { return _cache.Count; }
        }

        public bool StreamChunk(PeerInfo peer, int3 coord)
        {
            int version = _chunkProvider.GetChunkNetworkVersion(coord);

            if (version < 0)
            {
                return false;
            }

            // Check cache before serializing
            if (!_cache.TryGet(coord, version, out byte[] chunkData))
            {
                chunkData = _chunkProvider.SerializeChunk(coord);

                if (chunkData == null)
                {
                    return false;
                }

                _cache.Put(coord, version, chunkData);
            }

            ChunkDataMessage msg = new()
            {
                ChunkX = coord.x,
                ChunkY = coord.y,
                ChunkZ = coord.z,
                Payload = chunkData,
            };

            _server.SendTo(peer.ConnectionId, msg, PipelineId.FragmentedReliable);

            return true;
        }

        public void SendUnload(PeerInfo peer, int3 coord)
        {
            ChunkUnloadMessage msg = new()
            {
                ChunkX = coord.x,
                ChunkY = coord.y,
                ChunkZ = coord.z,
            };

            _server.SendTo(peer.ConnectionId, msg, PipelineId.ReliableSequenced);
        }

        /// <summary>
        /// Evicts the cache entry for the given coordinate.
        /// Call when the chunk is unloaded from the server.
        /// </summary>
        public void EvictCache(int3 coord)
        {
            _cache.Remove(coord);
        }

        /// <summary>
        /// Clears the entire cache. Call on session teardown.
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }
    }
}
