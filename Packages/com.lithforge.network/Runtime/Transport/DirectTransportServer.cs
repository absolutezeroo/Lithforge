using System;

namespace Lithforge.Network.Transport
{
    /// <summary>
    ///     Server side of a direct in-memory transport pair.
    ///     Uses a fixed ConnectionId(1) for the single local connection.
    ///     Data enqueued by the client appears in our inbound channel; data we send appears in the outbound channel.
    /// </summary>
    public sealed class DirectTransportServer : INetworkTransport
    {
        /// <summary>
        /// Fixed connection ID for the single local client connection.
        /// </summary>
        private static readonly ConnectionId LocalConnectionId = new(1);

        /// <summary>
        /// Channel for data flowing from client to this server.
        /// </summary>
        private readonly DirectChannel _inbound;

        /// <summary>
        /// Channel for data flowing from this server to the client.
        /// </summary>
        private readonly DirectChannel _outbound;

        /// <summary>
        /// Whether the local client is currently connected.
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
        /// Packet from the previous PollEvent call, pending return to ArrayPool.
        /// </summary>
        private DirectPacket _pendingReturn;

        /// <summary>
        /// Whether _pendingReturn has a pooled buffer to return.
        /// </summary>
        private bool _hasPendingReturn;

        /// <summary>
        /// Creates a new DirectTransportServer with the given inbound and outbound channels.
        /// </summary>
        internal DirectTransportServer(DirectChannel inbound, DirectChannel outbound)
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
        /// Marks the server as ready and queues a synthetic Connect event for the local client.
        /// </summary>
        public bool Listen(ushort port)
        {
            // No-op for direct transport. Mark the connect event as pending
            // so the first PollEvent call synthesizes a Connect event.
            _connectEventPending = true;

            return true;
        }

        /// <summary>
        /// Not supported; a server transport does not initiate outgoing connections.
        /// </summary>
        public ConnectionId Connect(string address, ushort port)
        {
            throw new InvalidOperationException("DirectTransportServer does not initiate connections.");
        }

        /// <summary>
        /// Disconnects the local client, enqueuing a Disconnect event on the client side.
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
        /// Returns pooled buffers from the previous data packet one cycle late (deferred return)
        /// to ensure the caller has consumed the data before it is recycled.
        /// </summary>
        public NetworkEventType PollEvent(
            out ConnectionId connectionId,
            out byte[] data,
            out int offset,
            out int length)
        {
            // Deferred return: return the previous packet's buffer now that the caller has consumed it
            if (_hasPendingReturn)
            {
                DirectChannel.ReturnPacket(_pendingReturn);
                _hasPendingReturn = false;
            }

            connectionId = LocalConnectionId;
            data = null;
            offset = 0;
            length = 0;

            // Synthesize Connect event on first poll after Listen
            if (_connectEventPending)
            {
                _connectEventPending = false;
                _connected = true;

                return NetworkEventType.Connect;
            }

            // Drain synthetic events (disconnect from client side)
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

                // Defer return to next PollEvent call so caller can read data safely
                _pendingReturn = packet;
                _hasPendingReturn = true;

                return NetworkEventType.Data;
            }

            return NetworkEventType.Empty;
        }

        /// <summary>
        /// Enqueues data into the outbound channel for the client to read.
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
        /// Disposes this transport, marking it as disconnected and returning any pending buffer.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _connected = false;

            if (_hasPendingReturn)
            {
                DirectChannel.ReturnPacket(_pendingReturn);
                _hasPendingReturn = false;
            }
        }
    }
}
