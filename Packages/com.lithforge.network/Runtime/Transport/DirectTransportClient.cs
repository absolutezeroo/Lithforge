using System;

namespace Lithforge.Network.Transport
{
    /// <summary>
    ///     Client side of a direct in-memory transport pair.
    ///     Mirror of DirectTransportServer with inbound/outbound swapped.
    ///     Connect() triggers a Connect event on both sides.
    /// </summary>
    public sealed class DirectTransportClient : INetworkTransport
    {
        /// <summary>
        /// Fixed connection ID used for the single server-side peer.
        /// </summary>
        private static readonly ConnectionId ServerConnectionId = new(1);

        /// <summary>
        /// Channel for data flowing from server to this client.
        /// </summary>
        private readonly DirectChannel _inbound;

        /// <summary>
        /// Channel for data flowing from this client to the server.
        /// </summary>
        private readonly DirectChannel _outbound;

        /// <summary>
        /// Whether this client is currently connected.
        /// </summary>
        private bool _connected;

        /// <summary>
        /// Whether a synthetic Connect event is pending for the next PollEvent call.
        /// </summary>
        private bool _connectEventPending;

        /// <summary>
        /// Whether this transport has been disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Creates a new DirectTransportClient with the given inbound and outbound channels.
        /// </summary>
        internal DirectTransportClient(DirectChannel inbound, DirectChannel outbound)
        {
            _inbound = inbound;
            _outbound = outbound;
        }

        /// <summary>
        /// No-op for direct transport; data is already in the queues.
        /// </summary>
        public void Update()
        {
            // No driver to pump — data is already in the queues.
        }

        /// <summary>
        /// Not supported; a client transport does not accept incoming connections.
        /// </summary>
        public bool Listen(ushort port)
        {
            throw new InvalidOperationException("DirectTransportClient does not listen for connections.");
        }

        /// <summary>
        /// Initiates a connection (immediately succeeds for in-memory transport).
        /// </summary>
        public ConnectionId Connect(string address, ushort port)
        {
            _connected = true;
            _connectEventPending = true;

            return ServerConnectionId;
        }

        /// <summary>
        /// Disconnects from the server, enqueuing a Disconnect event on the server side.
        /// </summary>
        public void Disconnect(ConnectionId connectionId)
        {
            if (!_connected)
            {
                return;
            }

            _connected = false;
            _outbound.EnqueueEvent(NetworkEventType.Disconnect);
        }

        /// <summary>
        /// Polls the next event: synthesized Connect first, then events, then data packets.
        /// </summary>
        public NetworkEventType PollEvent(
            out ConnectionId connectionId,
            out byte[] data,
            out int offset,
            out int length)
        {
            connectionId = ServerConnectionId;
            data = null;
            offset = 0;
            length = 0;

            // Synthesize Connect event on first poll after Connect()
            if (_connectEventPending)
            {
                _connectEventPending = false;

                return NetworkEventType.Connect;
            }

            // Drain synthetic events (disconnect from server side)
            if (_inbound.TryDequeueEvent(out NetworkEventType eventType))
            {
                if (eventType == NetworkEventType.Disconnect)
                {
                    _connected = false;
                }

                return eventType;
            }

            // Drain data packets
            if (_connected && _inbound.TryDequeue(out DirectPacket packet))
            {
                data = packet.Data;
                offset = packet.Offset;
                length = packet.Length;

                return NetworkEventType.Data;
            }

            return NetworkEventType.Empty;
        }

        /// <summary>
        /// Enqueues data into the outbound channel for the server to read.
        /// </summary>
        public bool Send(ConnectionId connectionId, int pipelineId, byte[] data, int offset, int length)
        {
            if (!_connected)
            {
                return false;
            }

            _outbound.Enqueue(data, offset, length);

            return true;
        }

        /// <summary>
        /// Disposes this transport, marking it as disconnected.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _connected = false;
        }
    }
}
