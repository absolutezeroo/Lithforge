using Lithforge.Network.Connection;
using Lithforge.Network.Messages;

using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Chunk streaming strategy for remote peers: serializes chunk data and sends
    ///     it over the network via the server transport.
    /// </summary>
    public sealed class NetworkChunkStreamingStrategy : IChunkStreamingStrategy
    {
        private readonly IServerChunkProvider _chunkProvider;

        private readonly INetworkServer _server;

        public NetworkChunkStreamingStrategy(INetworkServer server, IServerChunkProvider chunkProvider)
        {
            _server = server;
            _chunkProvider = chunkProvider;
        }

        public bool StreamChunk(PeerInfo peer, int3 coord)
        {
            byte[] chunkData = _chunkProvider.SerializeChunk(coord);

            if (chunkData == null)
            {
                return false;
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
    }
}
