using System;

using Lithforge.Core.Logging;
using Lithforge.Network.Connection;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using Lithforge.Network.SendQueue;
using Lithforge.Network.Transport;

namespace Lithforge.Network.Client
{
    /// <summary>
    ///     Production network client implementation.
    ///     Owns a single transport connection to the server, manages handshake and ping/pong.
    /// </summary>
    public sealed class NetworkClient : INetworkClient
    {
        private readonly ContentHash _contentHash;

        private readonly ILogger _logger;

        private readonly string _playerName;

        private bool _disposed;

        private float _lastPingTime;

        private float _lastUpdateTime;

        private bool _ownsTransport;

        private ReliableSendQueue _sendQueue;

        private ConnectionId _serverConnectionId;

        private ConnectionStateMachine _stateMachine;

        private INetworkTransport _transport;

        public NetworkClient(ILogger logger, ContentHash contentHash, string playerName)
        {
            _logger = logger;
            _contentHash = contentHash;
            _playerName = playerName ?? "";
        }

        public ConnectionState State
        {
            get
            {
                return _stateMachine?.Current ?? ConnectionState.Disconnected;
            }
        }

        public ushort LocalPlayerId { get; private set; }

        public uint ServerTickAtHandshake { get; private set; }

        public ulong WorldSeed { get; private set; }

        public float RoundTripTime { get; private set; }

        public MessageDispatcher Dispatcher { get; private set; }

        public Action OnHandshakeComplete { get; set; }

        public bool IsPlaying
        {
            get
            {
                return _stateMachine?.Current == ConnectionState.Playing;
            }
        }

        /// <summary>
        ///     Transitions the client to Playing state. Called when the server
        ///     sends GameReady, indicating chunk streaming is complete.
        /// </summary>
        public void TransitionToPlaying()
        {
            if (_stateMachine.Current == ConnectionState.Loading)
            {
                _stateMachine.Transition(ConnectionState.Playing, _lastUpdateTime);
                _logger.LogInfo("Client transitioned to Playing.");
            }
        }

        public bool IsConnected
        {
            get
            {
                return _stateMachine?.Current is ConnectionState.Handshaking
                    or ConnectionState.Loading or ConnectionState.Playing;
            }
        }

        public void Connect(string address, ushort port, float currentTime)
        {
            if (_transport != null)
            {
                _logger.LogWarning("NetworkClient.Connect called while already connected");

                return;
            }

            _lastUpdateTime = currentTime;
            _transport = new NetworkDriverWrapper(_logger);
            _ownsTransport = true;
            Dispatcher = new MessageDispatcher(_logger);
            _stateMachine = new ConnectionStateMachine();
            _sendQueue = new ReliableSendQueue(_logger);

            Dispatcher.OnConnect(OnConnected);
            Dispatcher.OnDisconnect(OnDisconnected);
            Dispatcher.RegisterHandler(MessageType.HandshakeResponse, OnHandshakeResponse);
            Dispatcher.RegisterHandler(MessageType.Pong, OnPong);
            Dispatcher.RegisterHandler(MessageType.Ping, OnServerPing);
            Dispatcher.RegisterHandler(MessageType.Disconnect, OnDisconnectMessage);

            _stateMachine.Transition(ConnectionState.Connecting, _lastUpdateTime);
            _serverConnectionId = _transport.Connect(address, port);

            _logger.LogInfo($"Connecting to {address}:{port}");
        }

        public void ConnectDirect(INetworkTransport transport, float currentTime)
        {
            if (_transport != null)
            {
                _logger.LogWarning("NetworkClient.ConnectDirect called while already connected");

                return;
            }

            _lastUpdateTime = currentTime;
            _transport = transport;
            _ownsTransport = false;
            Dispatcher = new MessageDispatcher(_logger);
            _stateMachine = new ConnectionStateMachine();
            _sendQueue = new ReliableSendQueue(_logger);

            Dispatcher.OnConnect(OnConnected);
            Dispatcher.OnDisconnect(OnDisconnected);
            Dispatcher.RegisterHandler(MessageType.HandshakeResponse, OnHandshakeResponse);
            Dispatcher.RegisterHandler(MessageType.Pong, OnPong);
            Dispatcher.RegisterHandler(MessageType.Ping, OnServerPing);
            Dispatcher.RegisterHandler(MessageType.Disconnect, OnDisconnectMessage);

            _stateMachine.Transition(ConnectionState.Connecting, _lastUpdateTime);
            // DirectTransportClient.Connect() returns the fixed server connection ID
            _serverConnectionId = _transport.Connect("localhost", 0);

            _logger.LogInfo("Connecting via direct transport");
        }

        public void Update(float currentTime)
        {
            if (_transport == null)
            {
                return;
            }

            _lastUpdateTime = currentTime;

            _transport.Update();
            Dispatcher.ProcessEvents(_transport);
            CheckTimeout(currentTime);
            SchedulePing(currentTime);
            _sendQueue.Flush(_transport);
        }

        public void Send(INetworkMessage message, int pipelineId)
        {
            if (_transport == null || !_serverConnectionId.IsValid)
            {
                return;
            }

            int totalBytes = MessageSerializer.WriteMessage(message, out byte[] buffer);
            bool success = _transport.Send(_serverConnectionId, pipelineId, buffer, 0, totalBytes);

            if (!success)
            {
                _logger.LogWarning($"Send failed for {message.Type}, queuing for retry");
                _sendQueue.Enqueue(_serverConnectionId, pipelineId, buffer, 0, totalBytes);
            }
        }

        public void Disconnect()
        {
            if (_transport == null)
            {
                return;
            }

            if (_stateMachine.Current != ConnectionState.Disconnected)
            {
                DisconnectMessage msg = new()
                {
                    Reason = DisconnectReason.Graceful,
                };

                Send(msg, PipelineId.ReliableSequenced);
                _stateMachine.Transition(ConnectionState.Disconnecting, _lastUpdateTime);
            }

            CleanUp();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                CleanUp();
            }
        }

        private void OnConnected(ConnectionId connectionId)
        {
            _serverConnectionId = connectionId;
            _stateMachine.Transition(ConnectionState.Handshaking, _lastUpdateTime);

            // Send handshake request immediately
            HandshakeRequestMessage request = new()
            {
                ProtocolVersion = NetworkConstants.ProtocolVersion, ContentHash = _contentHash, PlayerName = _playerName,
            };

            Send(request, PipelineId.ReliableSequenced);

            _logger.LogInfo($"Connected to server, sending handshake (contentHash={_contentHash})");
        }

        private void OnDisconnected(ConnectionId connectionId)
        {
            _logger.LogInfo("Disconnected from server");

            _stateMachine.Transition(ConnectionState.Disconnected, _lastUpdateTime);

            CleanUp();
        }

        private void OnHandshakeResponse(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            if (_stateMachine.Current != ConnectionState.Handshaking)
            {
                _logger.LogWarning("Received handshake response in unexpected state");

                return;
            }

            HandshakeResponseMessage response = HandshakeResponseMessage.Deserialize(data, offset, length);

            if (!response.Accepted)
            {
                _logger.LogError($"Handshake rejected: {response.RejectReason}");

                _stateMachine.Transition(ConnectionState.Disconnected, _lastUpdateTime);

                CleanUp();

                return;
            }

            LocalPlayerId = response.PlayerId;
            ServerTickAtHandshake = response.ServerTick;
            WorldSeed = response.WorldSeed;

            _stateMachine.Transition(ConnectionState.Loading, _lastUpdateTime);
            _logger.LogInfo(
                $"Handshake accepted: playerId={response.PlayerId}, " +
                $"serverTick={response.ServerTick}, seed={response.WorldSeed}");

            OnHandshakeComplete?.Invoke();
        }

        private void OnPong(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            PongMessage pong = PongMessage.Deserialize(data, offset, length);

            RoundTripTime = _lastUpdateTime - pong.EchoTimestamp;
        }

        private void OnServerPing(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            PingMessage ping = PingMessage.Deserialize(data, offset, length);
            PongMessage pong = new()
            {
                EchoTimestamp = ping.Timestamp, ServerTick = 0,
            };

            Send(pong, PipelineId.UnreliableSequenced);
        }

        private void OnDisconnectMessage(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            DisconnectMessage msg = DisconnectMessage.Deserialize(data, offset, length);

            _logger.LogInfo($"Server disconnected us: {msg.Reason}");

            _stateMachine.Transition(ConnectionState.Disconnected, _lastUpdateTime);

            CleanUp();
        }

        private void CheckTimeout(float currentTime)
        {
            ConnectionState state = _stateMachine.Current;

            if (state == ConnectionState.Connecting || state == ConnectionState.Handshaking)
            {
                if (_stateMachine.IsTimedOut(currentTime, NetworkConstants.HandshakeTimeoutSeconds))
                {
                    _logger.LogError("Connection timed out during handshake");

                    _stateMachine.Transition(ConnectionState.Disconnected, currentTime);

                    CleanUp();
                }
            }
        }

        private void SchedulePing(float currentTime)
        {
            ConnectionState state = _stateMachine.Current;

            if (state != ConnectionState.Playing && state != ConnectionState.Loading)
            {
                return;
            }

            if (currentTime - _lastPingTime >= NetworkConstants.PingIntervalSeconds)
            {
                _lastPingTime = currentTime;

                PingMessage ping = new()
                {
                    Timestamp = currentTime,
                };

                Send(ping, PipelineId.UnreliableSequenced);
            }
        }

        private void CleanUp()
        {
            _sendQueue?.Clear();

            if (_ownsTransport)
            {
                _transport?.Dispose();
            }

            _transport = null;
        }
    }
}
