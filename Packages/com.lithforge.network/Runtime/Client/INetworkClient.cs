using System;

using Lithforge.Network.Message;
using Lithforge.Network.Transport;

namespace Lithforge.Network.Client
{
    /// <summary>
    ///     Interface for the network client. Manages a single connection to a server,
    ///     handshake protocol, and message routing.
    /// </summary>
    public interface INetworkClient : IDisposable
    {
        /// <summary>
        ///     The current connection state.
        /// </summary>
        public ConnectionState State { get; }

        /// <summary>
        ///     The player ID assigned by the server during handshake, or 0 if not yet assigned.
        /// </summary>
        public ushort LocalPlayerId { get; }

        /// <summary>
        ///     The server tick received at handshake time, used as the initial sync point.
        /// </summary>
        public uint ServerTickAtHandshake { get; }

        /// <summary>
        ///     The world seed received from the server during handshake.
        /// </summary>
        public ulong WorldSeed { get; }

        /// <summary>
        ///     Current round-trip time in seconds to the server.
        /// </summary>
        public float RoundTripTime { get; }

        /// <summary>
        ///     The message dispatcher for registering external handlers.
        /// </summary>
        public MessageDispatcher Dispatcher { get; }

        /// <summary>
        ///     True when the client is fully connected and in Playing state.
        /// </summary>
        public bool IsPlaying { get; }

        /// <summary>
        ///     True when the UTP connection is established (Handshaking, Loading, or Playing).
        /// </summary>
        public bool IsConnected { get; }

        /// <summary>
        ///     Fired when the handshake completes successfully and the client enters Loading state.
        ///     Used by subsystems that need to defer initialization until the handshake is done.
        /// </summary>
        public Action OnHandshakeComplete { get; set; }

        /// <summary>
        ///     Initiates a connection to the given server address and port.
        /// </summary>
        /// <param name="currentTime">Current time (e.g. Time.realtimeSinceStartup) used to seed state-machine timestamps.</param>
        public void Connect(string address, ushort port, float currentTime);

        /// <summary>
        ///     Connects using an externally provided transport (e.g. DirectTransportClient for
        ///     always-server singleplayer). The transport should already have Connect() called.
        /// </summary>
        public void ConnectDirect(INetworkTransport transport, float currentTime);

        /// <summary>
        ///     Pumps the transport, processes events, checks timeouts, sends pings.
        ///     Must be called once per frame.
        /// </summary>
        public void Update(float currentTime);

        /// <summary>
        ///     Sends a message to the server on the given pipeline.
        /// </summary>
        public void Send(INetworkMessage message, int pipelineId);

        /// <summary>
        ///     Gracefully disconnects from the server.
        /// </summary>
        public void Disconnect();

        /// <summary>
        ///     Transitions the client from Loading to Playing state.
        ///     Called when the server sends GameReady.
        /// </summary>
        public void TransitionToPlaying();
    }
}
