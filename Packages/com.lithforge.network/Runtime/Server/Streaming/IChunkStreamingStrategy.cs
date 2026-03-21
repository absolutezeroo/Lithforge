using Lithforge.Network.Connection;

using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Strategy for delivering chunk data to a peer. Separates the concern of
    ///     how chunks are delivered (network serialize vs zero-copy local) from
    ///     the streaming manager's priority/rate logic.
    /// </summary>
    public interface IChunkStreamingStrategy
    {
        /// <summary>
        ///     Attempts to deliver chunk data for the given coordinate to the peer.
        ///     Returns true if the chunk was delivered, false if the chunk is not yet ready.
        /// </summary>
        public bool StreamChunk(PeerInfo peer, int3 coord);

        /// <summary>
        ///     Notifies the peer that the given chunk should be unloaded.
        /// </summary>
        public void SendUnload(PeerInfo peer, int3 coord);
    }
}
