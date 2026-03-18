using System;

using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using Lithforge.Network.Transport;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Interface for the network server. Manages transport, peer connections,
    ///     handshake protocol, and message routing.
    /// </summary>
    public interface INetworkServer : IDisposable
    {
        /// <summary>
        ///     Returns the number of connected peers (any state).
        /// </summary>
        public int PeerCount { get; }

        /// <summary>
        ///     The content hash this server requires clients to match.
        /// </summary>
        public ContentHash ServerContentHash { get; }

        /// <summary>
        ///     The message dispatcher for registering external handlers (P3/P4/P5).
        /// </summary>
        public MessageDispatcher Dispatcher { get; }

        /// <summary>
        ///     The current server tick, used for pong responses.
        /// </summary>
        public uint CurrentTick { get; set; }

        /// <summary>
        ///     The world seed, sent to clients during handshake.
        /// </summary>
        public ulong WorldSeed { get; set; }
        /// <summary>
        ///     Starts the server listening on the given port. Returns true on success.
        /// </summary>
        public bool Start(ushort port);

        /// <summary>
        ///     Starts the server with an externally provided transport (e.g. CompositeTransport
        ///     for always-server singleplayer). The transport must already be listening.
        /// </summary>
        public bool StartWithTransport(INetworkTransport transport);

        /// <summary>
        ///     Pumps the transport, processes events, checks timeouts, sends pings.
        ///     Must be called once per frame.
        /// </summary>
        public void Update(float currentTime);

        /// <summary>
        ///     Sends a message to a specific peer on the given pipeline.
        /// </summary>
        public void SendTo(ConnectionId connectionId, INetworkMessage message, int pipelineId);

        /// <summary>
        ///     Broadcasts a message to all peers in the Playing state.
        /// </summary>
        public void Broadcast(INetworkMessage message, int pipelineId);

        /// <summary>
        ///     Broadcasts a message to all Playing peers except the specified one.
        /// </summary>
        public void BroadcastExcept(ConnectionId excludeId, INetworkMessage message, int pipelineId);

        /// <summary>
        ///     Disconnects a peer with the given reason.
        /// </summary>
        public void DisconnectPeer(ConnectionId connectionId, DisconnectReason reason);

        /// <summary>
        ///     Shuts down the server: disconnects all peers and disposes the transport.
        /// </summary>
        public void Shutdown();

        /// <summary>
        ///     Gets the player ID assigned to a connection, or 0 if not found.
        /// </summary>
        public ushort GetPlayerId(ConnectionId connectionId);
    }
}
