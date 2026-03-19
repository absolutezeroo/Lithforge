using System.Buffers;
using System.Collections.Generic;

using Lithforge.Core.Logging;

using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Error;
using Unity.Networking.Transport.Utilities;

namespace Lithforge.Network.Transport
{
    /// <summary>
    ///     Production INetworkTransport implementation wrapping Unity Transport Package (UTP).
    ///     Creates a NetworkDriver with 4 pipelines: Unreliable, UnreliableSequenced,
    ///     ReliableSequenced, and FragmentedReliable.
    /// </summary>
    public sealed class NetworkDriverWrapper : INetworkTransport
    {
        /// <summary>
        /// Maps our sequential ConnectionId.Value to the UTP NetworkConnection handle.
        /// </summary>
        private readonly Dictionary<int, NetworkConnection> _connectionMap = new();

        /// <summary>
        /// Internal event buffer drained during Update, consumed by PollEvent.
        /// </summary>
        private readonly List<BufferedEvent> _eventBuffer = new();

        /// <summary>
        /// Logger instance for diagnostic messages.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Array of configured UTP pipelines indexed by PipelineId constants.
        /// </summary>
        private readonly NetworkPipeline[] _pipelines;

        /// <summary>
        /// Reverse map from UTP NetworkConnection to our sequential ConnectionId.Value.
        /// </summary>
        private readonly Dictionary<NetworkConnection, int> _reverseMap = new();

        /// <summary>
        /// Active UTP connections tracked for event polling.
        /// </summary>
        private NativeList<NetworkConnection> _connections;

        /// <summary>
        /// Whether this transport has been disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// The underlying Unity Transport Package network driver.
        /// </summary>
        private NetworkDriver _driver;

        /// <summary>
        /// Current read position in the event buffer for PollEvent.
        /// </summary>
        private int _eventIndex;

        /// <summary>
        /// Whether this wrapper is operating in server mode (accepting connections).
        /// </summary>
        private bool _isServer;

        /// <summary>
        /// Monotonically increasing counter for assigning collision-free connection IDs.
        /// </summary>
        private int _nextConnectionId;

        /// <summary>
        /// Creates a new NetworkDriverWrapper with configured pipelines and transport settings.
        /// </summary>
        public NetworkDriverWrapper(ILogger logger)
        {
            _logger = logger;

            NetworkSettings settings = new();
            settings.WithNetworkConfigParameters(
                sendQueueCapacity: NetworkConstants.SendQueueCapacity,
                receiveQueueCapacity: NetworkConstants.ReceiveQueueCapacity,
                disconnectTimeoutMS: NetworkConstants.DisconnectTimeoutMs,
                heartbeatTimeoutMS: NetworkConstants.HeartbeatTimeoutMs);
            settings.WithReliableStageParameters(
                windowSize: NetworkConstants.ReliableWindowSize);
            settings.WithFragmentationStageParameters(
                payloadCapacity: NetworkConstants.FragmentationPayloadCapacity);

            _driver = NetworkDriver.Create(settings);

            _pipelines = new NetworkPipeline[PipelineId.Count];
            _pipelines[PipelineId.Unreliable] = NetworkPipeline.Null;
            _pipelines[PipelineId.UnreliableSequenced] = _driver.CreatePipeline(
                typeof(UnreliableSequencedPipelineStage));
            _pipelines[PipelineId.ReliableSequenced] = _driver.CreatePipeline(
                typeof(ReliableSequencedPipelineStage));
            _pipelines[PipelineId.FragmentedReliable] = _driver.CreatePipeline(
                typeof(FragmentationPipelineStage),
                typeof(ReliableSequencedPipelineStage));

            _connections = new NativeList<NetworkConnection>(
                NetworkConstants.MaxConnections, Allocator.Persistent);
        }

        /// <summary>
        /// Pumps the UTP driver, buffers all events, and accepts new connections if in server mode.
        /// </summary>
        public void Update()
        {
            _driver.ScheduleUpdate().Complete();

            // Return pooled buffers from previous frame before clearing (Fix 3)
            ReturnEventBuffers();
            _eventBuffer.Clear();
            _eventIndex = 0;

            // Accept new connections (server only)
            if (_isServer)
            {
                NetworkConnection newConn;

                while ((newConn = _driver.Accept()) != default)
                {
                    _connections.Add(newConn);
                    ConnectionId connId = AllocateId(newConn);
                    _eventBuffer.Add(new BufferedEvent
                    {
                        EventType = NetworkEventType.Connect,
                        ConnectionId = connId,
                        Data = null,
                        Offset = 0,
                        Length = 0,
                        Pooled = false,
                    });
                }
            }

            // Drain events from all connections
            for (int i = 0; i < _connections.Length; i++)
            {
                NetworkConnection conn = _connections[i];

                if (!conn.IsCreated)
                {
                    continue;
                }

                NetworkEvent.Type eventType;

                while ((eventType = _driver.PopEventForConnection(
                           conn, out DataStreamReader stream)) !=
                       NetworkEvent.Type.Empty)
                {
                    ConnectionId connId = ResolveId(conn);

                    switch (eventType)
                    {
                        case NetworkEvent.Type.Connect:
                            _eventBuffer.Add(new BufferedEvent
                            {
                                EventType = NetworkEventType.Connect,
                                ConnectionId = connId,
                                Data = null,
                                Offset = 0,
                                Length = 0,
                                Pooled = false,
                            });
                            break;

                        case NetworkEvent.Type.Data:
                            int dataLength = stream.Length;
                            NativeArray<byte> nativeRead = new(
                                dataLength, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                            stream.ReadBytes(nativeRead);

                            // Rent from pool instead of allocating (Fix 3)
                            byte[] eventData = ArrayPool<byte>.Shared.Rent(dataLength);
                            NativeArray<byte>.Copy(nativeRead, 0, eventData, 0, dataLength);
                            nativeRead.Dispose();

                            _eventBuffer.Add(new BufferedEvent
                            {
                                EventType = NetworkEventType.Data,
                                ConnectionId = connId,
                                Data = eventData,
                                Offset = 0,
                                Length = dataLength,
                                Pooled = true,
                            });
                            break;

                        case NetworkEvent.Type.Disconnect:
                            _eventBuffer.Add(new BufferedEvent
                            {
                                EventType = NetworkEventType.Disconnect,
                                ConnectionId = connId,
                                Data = null,
                                Offset = 0,
                                Length = 0,
                                Pooled = false,
                            });
                            RemoveId(conn);
                            _connections.RemoveAtSwapBack(i);
                            i--;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Binds the driver to the given port and begins listening for incoming connections.
        /// </summary>
        public bool Listen(ushort port)
        {
            _isServer = true;
            NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4.WithPort(port);
            int result = _driver.Bind(endpoint);

            if (result != 0)
            {
                _logger.LogError($"Failed to bind to port {port}: error {result}");
                return false;
            }

            result = _driver.Listen();

            if (result != 0)
            {
                _logger.LogError($"Failed to listen on port {port}: error {result}");
                return false;
            }

            _logger.LogInfo($"Listening on port {port}");
            return true;
        }

        /// <summary>
        /// Initiates a connection to the given address and port. Returns the assigned ConnectionId.
        /// </summary>
        public ConnectionId Connect(string address, ushort port)
        {
            NetworkEndpoint endpoint = NetworkEndpoint.Parse(address, port);
            NetworkConnection conn = _driver.Connect(endpoint);
            _connections.Add(conn);
            return AllocateId(conn);
        }

        /// <summary>
        /// Gracefully disconnects the given connection and removes its ID mapping.
        /// </summary>
        public void Disconnect(ConnectionId connectionId)
        {
            if (!TryGetConnection(connectionId, out NetworkConnection conn))
            {
                return;
            }

            _driver.Disconnect(conn);
            RemoveId(conn);

            for (int i = 0; i < _connections.Length; i++)
            {
                if (_connections[i].Equals(conn))
                {
                    _connections.RemoveAtSwapBack(i);
                    return;
                }
            }
        }

        /// <summary>
        /// Returns the next buffered event, advancing the read cursor.
        /// </summary>
        public NetworkEventType PollEvent(
            out ConnectionId connectionId,
            out byte[] data,
            out int offset,
            out int length)
        {
            if (_eventIndex >= _eventBuffer.Count)
            {
                connectionId = ConnectionId.Invalid;
                data = null;
                offset = 0;
                length = 0;
                return NetworkEventType.Empty;
            }

            BufferedEvent evt = _eventBuffer[_eventIndex];
            _eventIndex++;

            connectionId = evt.ConnectionId;
            data = evt.Data;
            offset = evt.Offset;
            length = evt.Length;
            return evt.EventType;
        }

        /// <summary>
        /// Sends data on the specified pipeline. Returns false if the send queue is full.
        /// </summary>
        public bool Send(ConnectionId connectionId, int pipelineId, byte[] data, int offset, int length)
        {
            if (!TryGetConnection(connectionId, out NetworkConnection conn))
            {
                _logger.LogWarning($"Send failed: connection {connectionId} not found");
                return false;
            }

            NetworkPipeline pipeline = _pipelines[pipelineId];
            int result = _driver.BeginSend(pipeline, conn, out DataStreamWriter writer);

            if (result != (int)StatusCode.Success)
            {
                if (result == NetworkConstants.SendQueueFullError)
                {
                    return false;
                }

                _logger.LogError($"BeginSend failed: error {result}");
                return false;
            }

            NativeArray<byte> nativeWrite = new(
                length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte>.Copy(data, offset, nativeWrite, 0, length);
            writer.WriteBytes(nativeWrite);
            nativeWrite.Dispose();

            // Fix 1 — check EndSend return value; negative means send queue full
            int endResult = _driver.EndSend(writer);

            if (endResult < 0)
            {
                _logger.LogWarning($"EndSend failed: error {endResult} for connection {connectionId}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Disposes the UTP driver, disconnecting all connections and freeing native memory.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ReturnEventBuffers();

            if (_connections.IsCreated)
            {
                for (int i = 0; i < _connections.Length; i++)
                {
                    if (_connections[i].IsCreated)
                    {
                        _driver.Disconnect(_connections[i]);
                    }
                }

                _connections.Dispose();
            }

            if (_driver.IsCreated)
            {
                _driver.Dispose();
            }

            _connectionMap.Clear();
            _reverseMap.Clear();
        }

        // --- Connection ID management (Fix 2) ---

        /// <summary>
        /// Allocates a new sequential ConnectionId for the given UTP NetworkConnection.
        /// </summary>
        private ConnectionId AllocateId(NetworkConnection conn)
        {
            int id = _nextConnectionId++;
            _connectionMap[id] = conn;
            _reverseMap[conn] = id;
            return new ConnectionId(id);
        }

        /// <summary>
        /// Resolves the ConnectionId for a UTP NetworkConnection, allocating on-the-fly as fallback.
        /// </summary>
        private ConnectionId ResolveId(NetworkConnection conn)
        {
            if (_reverseMap.TryGetValue(conn, out int id))
            {
                return new ConnectionId(id);
            }

            // Shouldn't happen — allocate on the fly as a safety fallback
            return AllocateId(conn);
        }

        /// <summary>
        /// Looks up the UTP NetworkConnection for the given ConnectionId. Returns false if not found.
        /// </summary>
        private bool TryGetConnection(ConnectionId connId, out NetworkConnection conn)
        {
            return _connectionMap.TryGetValue(connId.Value, out conn);
        }

        /// <summary>
        /// Removes the ID mapping for the given UTP NetworkConnection.
        /// </summary>
        private void RemoveId(NetworkConnection conn)
        {
            if (_reverseMap.TryGetValue(conn, out int id))
            {
                _reverseMap.Remove(conn);
                _connectionMap.Remove(id);
            }
        }

        // --- ArrayPool buffer management (Fix 3) ---

        /// <summary>
        /// Returns all pooled byte arrays from the event buffer back to ArrayPool.
        /// </summary>
        private void ReturnEventBuffers()
        {
            for (int i = 0; i < _eventBuffer.Count; i++)
            {
                BufferedEvent evt = _eventBuffer[i];

                if (evt is
                    {
                        Pooled: true,
                        Data: not null,
                    })
                {
                    ArrayPool<byte>.Shared.Return(evt.Data);
                }
            }
        }

        /// <summary>
        /// Internal struct storing a buffered network event for deferred consumption by PollEvent.
        /// </summary>
        private struct BufferedEvent
        {
            /// <summary>
            /// The type of network event.
            /// </summary>
            public NetworkEventType EventType;

            /// <summary>
            /// The connection this event belongs to.
            /// </summary>
            public ConnectionId ConnectionId;

            /// <summary>
            /// Raw data buffer (null for non-Data events).
            /// </summary>
            public byte[] Data;

            /// <summary>
            /// Start offset within the data buffer.
            /// </summary>
            public int Offset;

            /// <summary>
            /// Number of valid bytes starting from offset.
            /// </summary>
            public int Length;

            /// <summary>
            /// Whether the Data array was rented from ArrayPool and needs to be returned.
            /// </summary>
            public bool Pooled;
        }
    }
}
