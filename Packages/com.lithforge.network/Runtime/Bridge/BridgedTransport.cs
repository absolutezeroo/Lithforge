using System;
using System.Collections.Generic;

using Lithforge.Network.Transport;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     <see cref="INetworkTransport" /> implementation consumed by <see cref="Server.NetworkServer" />
    ///     on the server thread. Drains inbound events from the bridge queue on Update(),
    ///     and enqueues outbound sends for the main thread to flush.
    ///     Listen/Connect are no-ops — the real transport is managed by the main thread.
    /// </summary>
    internal sealed class BridgedTransport : INetworkTransport
    {
        /// <summary>Shared cross-thread state.</summary>
        private readonly ServerThreadBridge _bridge;

        /// <summary>Staging list for events drained from the concurrent queue during Update().</summary>
        private readonly List<NetworkEventEnvelope> _staged = new();

        /// <summary>Cursor into <see cref="_staged" /> for sequential PollEvent calls.</summary>
        private int _pollIndex;

        /// <summary>Creates a bridged transport backed by the given shared bridge.</summary>
        public BridgedTransport(ServerThreadBridge bridge)
        {
            _bridge = bridge;
        }

        /// <summary>Drains all pending inbound events from the bridge queue into the staging list.</summary>
        public void Update()
        {
            _staged.Clear();
            _pollIndex = 0;

            while (_bridge.InboundEvents.TryDequeue(out NetworkEventEnvelope envelope))
            {
                _staged.Add(envelope);
            }
        }

        /// <summary>No-op — real transport listens on the main thread.</summary>
        public bool Listen(ushort port)
        {
            return true;
        }

        /// <summary>No-op — client connections are handled on the main thread.</summary>
        public ConnectionId Connect(string address, ushort port)
        {
            return new ConnectionId(-1);
        }

        /// <summary>Enqueues a disconnect as a send request for the main thread to process.</summary>
        public void Disconnect(ConnectionId connectionId)
        {
            // Enqueue a special send with negative pipeline to signal disconnect
            _bridge.OutboundSends.Enqueue(new SendRequest
            {
                ConnectionId = connectionId,
                PipelineId = -1, // sentinel: disconnect request
                Data = null,
                Offset = 0,
                Length = 0,
            });
        }

        /// <summary>
        ///     Returns the next staged event, or Empty when all events have been consumed.
        /// </summary>
        public NetworkEventType PollEvent(
            out ConnectionId connectionId,
            out byte[] data,
            out int offset,
            out int length)
        {
            if (_pollIndex >= _staged.Count)
            {
                connectionId = default;
                data = null;
                offset = 0;
                length = 0;

                return NetworkEventType.Empty;
            }

            NetworkEventEnvelope envelope = _staged[_pollIndex++];
            connectionId = envelope.ConnectionId;
            data = envelope.Data;
            offset = 0;
            length = envelope.Length;

            return envelope.EventType;
        }

        /// <summary>
        ///     Enqueues an outbound send for the main thread to deliver via the real transport.
        /// </summary>
        public bool Send(ConnectionId connectionId, int pipelineId, byte[] data, int offset, int length)
        {
            // Defensive copy: the caller may reuse the buffer
            byte[] copy = new byte[length];

            Buffer.BlockCopy(data, offset, copy, 0, length);

            _bridge.OutboundSends.Enqueue(new SendRequest
            {
                ConnectionId = connectionId,
                PipelineId = pipelineId,
                Data = copy,
                Offset = 0,
                Length = length,
            });

            return true;
        }

        /// <summary>No resources to dispose — the bridge owns the semaphores.</summary>
        public void Dispose()
        {
        }
    }
}
