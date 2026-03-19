using System;
using System.Collections.Generic;

namespace Lithforge.Network.Transport
{
    /// <summary>
    ///     Wraps multiple INetworkTransport instances into a single transport.
    ///     Remaps ConnectionIds so each inner transport's connections get unique composite IDs.
    ///     Used for concurrent local (DirectTransport) + remote (UTP) connections.
    /// </summary>
    public sealed class CompositeTransport : INetworkTransport
    {
        /// <summary>
        /// The list of inner transports aggregated by this composite.
        /// </summary>
        private readonly List<INetworkTransport> _transports = new();

        /// <summary>
        /// Maps composite ConnectionId values to their inner transport and raw connection ID.
        /// </summary>
        private readonly Dictionary<int, RouteEntry> _routing = new();

        /// <summary>
        /// Monotonically increasing counter for assigning unique composite connection IDs.
        /// </summary>
        private int _nextCompositeId = 1;

        /// <summary>
        /// Round-robin index for polling inner transports.
        /// </summary>
        private int _pollIndex;

        /// <summary>
        /// Whether this composite transport has been disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Adds an inner transport to this composite. Events from this transport will be merged.
        /// </summary>
        public void AddTransport(INetworkTransport transport)
        {
            _transports.Add(transport);
        }

        /// <summary>
        /// Removes an inner transport and all its routing entries from this composite.
        /// </summary>
        public void RemoveTransport(INetworkTransport transport)
        {
            _transports.Remove(transport);

            // Remove all routing entries for this transport
            List<int> toRemove = new();

            foreach (KeyValuePair<int, RouteEntry> kvp in _routing)
            {
                if (ReferenceEquals(kvp.Value.Transport, transport))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (int id in toRemove)
            {
                _routing.Remove(id);
            }
        }

        /// <summary>
        /// Pumps all inner transports.
        /// </summary>
        public void Update()
        {
            for (int i = 0; i < _transports.Count; i++)
            {
                _transports[i].Update();
            }
        }

        /// <summary>
        /// Not supported on CompositeTransport. Inner transports must listen before being added.
        /// </summary>
        public bool Listen(ushort port)
        {
            // Composite doesn't listen directly — inner transports handle this.
            throw new InvalidOperationException(
                "CompositeTransport does not listen directly. Call Listen on inner transports before adding them.");
        }

        /// <summary>
        /// Not supported on CompositeTransport. Inner transports must connect before being added.
        /// </summary>
        public ConnectionId Connect(string address, ushort port)
        {
            // Composite doesn't connect directly — inner transports handle this.
            throw new InvalidOperationException(
                "CompositeTransport does not connect directly. Call Connect on inner transports before adding them.");
        }

        /// <summary>
        /// Disconnects the connection identified by the composite ID by forwarding to the inner transport.
        /// </summary>
        public void Disconnect(ConnectionId connectionId)
        {
            if (!_routing.TryGetValue(connectionId.Value, out RouteEntry entry))
            {
                return;
            }

            entry.Transport.Disconnect(entry.RawConnectionId);
            _routing.Remove(connectionId.Value);
        }

        /// <summary>
        /// Polls the next event using round-robin across inner transports, remapping connection IDs.
        /// </summary>
        public NetworkEventType PollEvent(
            out ConnectionId connectionId,
            out byte[] data,
            out int offset,
            out int length)
        {
            connectionId = ConnectionId.Invalid;
            data = null;
            offset = 0;
            length = 0;

            if (_transports.Count == 0)
            {
                return NetworkEventType.Empty;
            }

            // Round-robin across inner transports until all return Empty.
            int checked_ = 0;

            while (checked_ < _transports.Count)
            {
                INetworkTransport transport = _transports[_pollIndex];
                _pollIndex = (_pollIndex + 1) % _transports.Count;
                checked_++;

                NetworkEventType eventType = transport.PollEvent(
                    out ConnectionId rawConnectionId,
                    out byte[] rawData,
                    out int rawOffset,
                    out int rawLength);

                if (eventType == NetworkEventType.Empty)
                {
                    continue;
                }

                if (eventType == NetworkEventType.Connect)
                {
                    int compositeId = _nextCompositeId++;
                    _routing[compositeId] = new RouteEntry(transport, rawConnectionId);
                    connectionId = new ConnectionId(compositeId);

                    return NetworkEventType.Connect;
                }

                // For Data and Disconnect, look up the composite ID from the raw ID
                int foundCompositeId = FindCompositeId(transport, rawConnectionId);
                if (foundCompositeId < 0)
                {
                    // Unknown connection — skip
                    continue;
                }

                connectionId = new ConnectionId(foundCompositeId);

                if (eventType == NetworkEventType.Disconnect)
                {
                    _routing.Remove(foundCompositeId);

                    return NetworkEventType.Disconnect;
                }

                // Data event
                data = rawData;
                offset = rawOffset;
                length = rawLength;

                return NetworkEventType.Data;
            }

            return NetworkEventType.Empty;
        }

        /// <summary>
        /// Sends data by looking up the composite ID and forwarding to the correct inner transport.
        /// </summary>
        public bool Send(ConnectionId connectionId, int pipelineId, byte[] data, int offset, int length)
        {
            if (!_routing.TryGetValue(connectionId.Value, out RouteEntry entry))
            {
                return false;
            }

            return entry.Transport.Send(entry.RawConnectionId, pipelineId, data, offset, length);
        }

        /// <summary>
        /// Looks up the composite ID for a raw connection on a specific inner transport.
        /// </summary>
        public ConnectionId GetCompositeId(INetworkTransport transport, ConnectionId rawConnectionId)
        {
            int found = FindCompositeId(transport, rawConnectionId);

            return found >= 0 ? new ConnectionId(found) : ConnectionId.Invalid;
        }

        /// <summary>
        /// Disposes this composite (clears routing) without disposing inner transports.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _routing.Clear();

            // Do NOT dispose inner transports — they are owned by their creators.
        }

        /// <summary>
        /// Searches the routing table for the composite ID matching a given inner transport and raw connection.
        /// </summary>
        private int FindCompositeId(INetworkTransport transport, ConnectionId rawConnectionId)
        {
            foreach (KeyValuePair<int, RouteEntry> kvp in _routing)
            {
                if (ReferenceEquals(kvp.Value.Transport, transport) &&
                    kvp.Value.RawConnectionId.Value == rawConnectionId.Value)
                {
                    return kvp.Key;
                }
            }

            return -1;
        }

        /// <summary>
        /// Maps a composite connection ID to its inner transport and the raw connection ID within that transport.
        /// </summary>
        private readonly struct RouteEntry
        {
            /// <summary>
            /// The inner transport that owns this connection.
            /// </summary>
            public readonly INetworkTransport Transport;

            /// <summary>
            /// The connection ID within the inner transport.
            /// </summary>
            public readonly ConnectionId RawConnectionId;

            /// <summary>
            /// Creates a new route entry.
            /// </summary>
            public RouteEntry(INetworkTransport transport, ConnectionId rawConnectionId)
            {
                Transport = transport;
                RawConnectionId = rawConnectionId;
            }
        }
    }
}
