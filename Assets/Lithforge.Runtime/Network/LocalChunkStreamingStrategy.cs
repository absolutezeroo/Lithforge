using System;

using Lithforge.Network.Connection;
using Lithforge.Network.Server;

using Unity.Mathematics;

namespace Lithforge.Runtime.Network
{
    /// <summary>
    ///     Chunk streaming strategy for the local peer in always-server mode.
    ///     Zero-copy path: the local client shares ChunkManager with the server,
    ///     so no serialization is needed. Simply marks chunks as loaded when they
    ///     reach Generated state and invokes callbacks for the client handler.
    /// </summary>
    public sealed class LocalChunkStreamingStrategy : IChunkStreamingStrategy
    {
        private readonly IServerChunkProvider _chunkProvider;

        private readonly Action<int3> _onChunkReady;

        private readonly Action<int3> _onChunkUnload;

        public LocalChunkStreamingStrategy(
            IServerChunkProvider chunkProvider,
            Action<int3> onChunkReady,
            Action<int3> onChunkUnload)
        {
            _chunkProvider = chunkProvider;
            _onChunkReady = onChunkReady;
            _onChunkUnload = onChunkUnload;
        }

        public bool StreamChunk(PeerInfo peer, int3 coord)
        {
            if (!_chunkProvider.IsChunkGenerated(coord))
            {
                return false;
            }

            _onChunkReady?.Invoke(coord);

            return true;
        }

        public void SendUnload(PeerInfo peer, int3 coord)
        {
            _onChunkUnload?.Invoke(coord);
        }
    }
}
