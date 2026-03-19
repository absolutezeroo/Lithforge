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
        /// <summary>
        /// Cache of serialized chunk data keyed by coordinate and version.
        /// </summary>
        private readonly CompressedChunkCache _cache = new();

        /// <summary>
        /// Provider for querying chunk readiness and serializing chunk data.
        /// </summary>
        private readonly IServerChunkProvider _chunkProvider;

        /// <summary>
        /// The network server used to send messages to peers.
        /// </summary>
        private readonly INetworkServer _server;

        /// <summary>
        /// Creates a new NetworkChunkStreamingStrategy with the given server and chunk provider.
        /// </summary>
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

        /// <summary>
        /// Serializes and sends chunk data to the peer. Uses the cache to avoid redundant serialization.
        /// </summary>
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

        /// <summary>
        /// Sends a ChunkUnloadMessage to the peer for the given coordinate.
        /// </summary>
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
