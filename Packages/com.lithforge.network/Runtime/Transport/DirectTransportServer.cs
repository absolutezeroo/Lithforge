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
        private static readonly ConnectionId LocalConnectionId = new(1);

        private readonly DirectChannel _inbound;

        private readonly DirectChannel _outbound;

        private bool _connected;

        private bool _connectEventPending;

        private bool _disposed;

        internal DirectTransportServer(DirectChannel inbound, DirectChannel outbound)
        {
            _inbound = inbound;
            _outbound = outbound;
        }

        public void Update()
        {
            // No driver to pump — data is already in the queues.
        }

        public bool Listen(ushort port)
        {
            // No-op for direct transport. Mark the connect event as pending
            // so the first PollEvent call synthesizes a Connect event.
            _connectEventPending = true;

            return true;
        }

        public ConnectionId Connect(string address, ushort port)
        {
            throw new InvalidOperationException("DirectTransportServer does not initiate connections.");
        }

        public void Disconnect(ConnectionId connectionId)
        {
            if (!_connected)
            {
                return;
            }

            _connected = false;
            _outbound.EnqueueEvent(NetworkEventType.Disconnect);
        }

        public NetworkEventType PollEvent(
            out ConnectionId connectionId,
            out byte[] data,
            out int offset,
            out int length)
        {
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

                return NetworkEventType.Data;
            }

            return NetworkEventType.Empty;
        }

        public bool Send(ConnectionId connectionId, int pipelineId, byte[] data, int offset, int length)
        {
            if (!_connected)
            {
                return false;
            }

            _outbound.Enqueue(data, offset, length);

            return true;
        }

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
