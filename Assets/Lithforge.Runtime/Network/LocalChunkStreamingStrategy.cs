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
        /// <summary>Server chunk provider for checking generation state.</summary>
        private readonly IServerChunkProvider _chunkProvider;

        /// <summary>Callback invoked when a chunk reaches Generated state.</summary>
        private readonly Action<int3> _onChunkReady;

        /// <summary>Callback invoked when a chunk is unloaded from the client's view.</summary>
        private readonly Action<int3> _onChunkUnload;

        /// <summary>Creates the strategy with chunk provider and lifecycle callbacks.</summary>
        public LocalChunkStreamingStrategy(
            IServerChunkProvider chunkProvider,
            Action<int3> onChunkReady,
            Action<int3> onChunkUnload)
        {
            _chunkProvider = chunkProvider;
            _onChunkReady = onChunkReady;
            _onChunkUnload = onChunkUnload;
        }

        /// <summary>Checks if a chunk is generated and invokes the ready callback if so.</summary>
        public bool StreamChunk(PeerInfo peer, int3 coord)
        {
            if (!_chunkProvider.IsChunkGenerated(coord))
            {
                return false;
            }

            _onChunkReady?.Invoke(coord);

            return true;
        }

        /// <summary>Invokes the unload callback for the given chunk coordinate.</summary>
        public void SendUnload(PeerInfo peer, int3 coord)
        {
            _onChunkUnload?.Invoke(coord);
        }
    }
}
