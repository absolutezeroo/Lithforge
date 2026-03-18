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
        // Internal event buffer drained during Update, consumed by PollEvent
        private readonly List<BufferedEvent> _eventBuffer = new();
        private readonly ILogger _logger;
        private readonly NetworkPipeline[] _pipelines;

        // Sequential connection ID allocation (Fix 2 — avoids GetHashCode collision risk)
        private int _nextConnectionId;
        private readonly Dictionary<int, NetworkConnection> _connectionMap = new();
        private readonly Dictionary<NetworkConnection, int> _reverseMap = new();

        private NativeList<NetworkConnection> _connections;
        private bool _disposed;
        private NetworkDriver _driver;
        private int _eventIndex;
        private bool _isServer;

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

        public ConnectionId Connect(string address, ushort port)
        {
            NetworkEndpoint endpoint = NetworkEndpoint.Parse(address, port);
            NetworkConnection conn = _driver.Connect(endpoint);
            _connections.Add(conn);
            return AllocateId(conn);
        }

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

        private ConnectionId AllocateId(NetworkConnection conn)
        {
            int id = _nextConnectionId++;
            _connectionMap[id] = conn;
            _reverseMap[conn] = id;
            return new ConnectionId(id);
        }

        private ConnectionId ResolveId(NetworkConnection conn)
        {
            if (_reverseMap.TryGetValue(conn, out int id))
            {
                return new ConnectionId(id);
            }

            // Shouldn't happen — allocate on the fly as a safety fallback
            return AllocateId(conn);
        }

        private bool TryGetConnection(ConnectionId connId, out NetworkConnection conn)
        {
            return _connectionMap.TryGetValue(connId.Value, out conn);
        }

        private void RemoveId(NetworkConnection conn)
        {
            if (_reverseMap.TryGetValue(conn, out int id))
            {
                _reverseMap.Remove(conn);
                _connectionMap.Remove(id);
            }
        }

        // --- ArrayPool buffer management (Fix 3) ---

        private void ReturnEventBuffers()
        {
            for (int i = 0; i < _eventBuffer.Count; i++)
            {
                BufferedEvent evt = _eventBuffer[i];

                if (evt.Pooled && evt.Data != null)
                {
                    ArrayPool<byte>.Shared.Return(evt.Data);
                }
            }
        }

        private struct BufferedEvent
        {
            public NetworkEventType EventType;
            public ConnectionId ConnectionId;
            public byte[] Data;
            public int Offset;
            public int Length;
            public bool Pooled;
        }
    }
}
