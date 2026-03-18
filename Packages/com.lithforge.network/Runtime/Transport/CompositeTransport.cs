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
        private readonly List<INetworkTransport> _transports = new();

        private readonly Dictionary<int, RouteEntry> _routing = new();

        private int _nextCompositeId = 1;

        private int _pollIndex;

        private bool _disposed;

        public void AddTransport(INetworkTransport transport)
        {
            _transports.Add(transport);
        }

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

        public void Update()
        {
            for (int i = 0; i < _transports.Count; i++)
            {
                _transports[i].Update();
            }
        }

        public bool Listen(ushort port)
        {
            // Composite doesn't listen directly — inner transports handle this.
            throw new InvalidOperationException(
                "CompositeTransport does not listen directly. Call Listen on inner transports before adding them.");
        }

        public ConnectionId Connect(string address, ushort port)
        {
            // Composite doesn't connect directly — inner transports handle this.
            throw new InvalidOperationException(
                "CompositeTransport does not connect directly. Call Connect on inner transports before adding them.");
        }

        public void Disconnect(ConnectionId connectionId)
        {
            if (!_routing.TryGetValue(connectionId.Value, out RouteEntry entry))
            {
                return;
            }

            entry.Transport.Disconnect(entry.RawConnectionId);
            _routing.Remove(connectionId.Value);
        }

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

        public bool Send(ConnectionId connectionId, int pipelineId, byte[] data, int offset, int length)
        {
            if (!_routing.TryGetValue(connectionId.Value, out RouteEntry entry))
            {
                return false;
            }

            return entry.Transport.Send(entry.RawConnectionId, pipelineId, data, offset, length);
        }

        public ConnectionId GetCompositeId(INetworkTransport transport, ConnectionId rawConnectionId)
        {
            int found = FindCompositeId(transport, rawConnectionId);

            return found >= 0 ? new ConnectionId(found) : ConnectionId.Invalid;
        }

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

        private readonly struct RouteEntry
        {
            public readonly INetworkTransport Transport;
            public readonly ConnectionId RawConnectionId;

            public RouteEntry(INetworkTransport transport, ConnectionId rawConnectionId)
            {
                Transport = transport;
                RawConnectionId = rawConnectionId;
            }
        }
    }
}
