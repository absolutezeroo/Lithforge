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
        private NativeList<NetworkConnection> _connections;
        private bool _disposed;
        private NetworkDriver _driver;
        private int _eventIndex;
        private bool _isServer;
        private readonly NetworkPipeline[] _pipelines;

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

            _eventBuffer.Clear();
            _eventIndex = 0;

            // Accept new connections (server only)
            if (_isServer)
            {
                NetworkConnection newConn;

                while ((newConn = _driver.Accept()) != default)
                {
                    _connections.Add(newConn);
                    _eventBuffer.Add(new BufferedEvent
                    {
                        EventType = NetworkEventType.Connect,
                        ConnectionId = new ConnectionId(newConn.GetHashCode()),
                        Data = null,
                        Offset = 0,
                        Length = 0,
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
                    ConnectionId connId = new(conn.GetHashCode());

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
                            });
                            break;

                        case NetworkEvent.Type.Data:
                            int dataLength = stream.Length;
                            NativeArray<byte> nativeRead = new(
                                dataLength, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                            stream.ReadBytes(nativeRead);

                            byte[] eventData = new byte[dataLength];
                            nativeRead.CopyTo(eventData);
                            nativeRead.Dispose();

                            _eventBuffer.Add(new BufferedEvent
                            {
                                EventType = NetworkEventType.Data,
                                ConnectionId = connId,
                                Data = eventData,
                                Offset = 0,
                                Length = dataLength,
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
                            });
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
            return new ConnectionId(conn.GetHashCode());
        }

        public void Disconnect(ConnectionId connectionId)
        {
            for (int i = 0; i < _connections.Length; i++)
            {
                if (_connections[i].GetHashCode() == connectionId.Value)
                {
                    _driver.Disconnect(_connections[i]);
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
            for (int i = 0; i < _connections.Length; i++)
            {
                if (_connections[i].GetHashCode() == connectionId.Value)
                {
                    NetworkPipeline pipeline = _pipelines[pipelineId];
                    int result = _driver.BeginSend(pipeline, _connections[i], out DataStreamWriter writer);

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

                    _driver.EndSend(writer);
                    return true;
                }
            }

            _logger.LogWarning($"Send failed: connection {connectionId} not found");
            return false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

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
        }

        private struct BufferedEvent
        {
            public NetworkEventType EventType;
            public ConnectionId ConnectionId;
            public byte[] Data;
            public int Offset;
            public int Length;
        }
    }
}
