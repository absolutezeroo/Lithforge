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
        private static readonly ConnectionId ServerConnectionId = new(1);

        private readonly DirectChannel _inbound;

        private readonly DirectChannel _outbound;

        private bool _connected;

        private bool _connectEventPending;

        private bool _disposed;

        internal DirectTransportClient(DirectChannel inbound, DirectChannel outbound)
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
            throw new InvalidOperationException("DirectTransportClient does not listen for connections.");
        }

        public ConnectionId Connect(string address, ushort port)
        {
            _connected = true;
            _connectEventPending = true;

            return ServerConnectionId;
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
