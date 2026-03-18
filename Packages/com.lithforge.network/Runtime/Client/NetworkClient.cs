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
    /// Production network client implementation.
    /// Owns a single transport connection to the server, manages handshake and ping/pong.
    /// </summary>
    public sealed class NetworkClient : INetworkClient
    {
        private readonly ILogger _logger;
        private readonly ContentHash _contentHash;
        private readonly string _playerName;

        private INetworkTransport _transport;
        private MessageDispatcher _dispatcher;
        private ConnectionStateMachine _stateMachine;
        private ReliableSendQueue _sendQueue;
        private ConnectionId _serverConnectionId;
        private float _lastPingTime;
        private float _lastUpdateTime;
        private bool _disposed;

        public ConnectionState State
        {
            get { return _stateMachine != null ? _stateMachine.Current : ConnectionState.Disconnected; }
        }

        public ushort LocalPlayerId { get; private set; }
        public uint ServerTickAtHandshake { get; private set; }
        public ulong WorldSeed { get; private set; }
        public float RoundTripTime { get; private set; }

        public MessageDispatcher Dispatcher
        {
            get { return _dispatcher; }
        }

        public NetworkClient(ILogger logger, ContentHash contentHash, string playerName)
        {
            _logger = logger;
            _contentHash = contentHash;
            _playerName = playerName ?? "";
        }

        public void Connect(string address, ushort port)
        {
            if (_transport != null)
            {
                _logger.LogWarning("NetworkClient.Connect called while already connected");
                return;
            }

            _transport = new NetworkDriverWrapper(_logger);
            _dispatcher = new MessageDispatcher(_logger);
            _stateMachine = new ConnectionStateMachine();
            _sendQueue = new ReliableSendQueue(_logger);

            _dispatcher.OnConnect(OnConnected);
            _dispatcher.OnDisconnect(OnDisconnected);
            _dispatcher.RegisterHandler(MessageType.HandshakeResponse, OnHandshakeResponse);
            _dispatcher.RegisterHandler(MessageType.Pong, OnPong);
            _dispatcher.RegisterHandler(MessageType.Disconnect, OnDisconnectMessage);

            _stateMachine.Transition(ConnectionState.Connecting, 0f);
            _serverConnectionId = _transport.Connect(address, port);

            _logger.LogInfo($"Connecting to {address}:{port}");
        }

        public void Update(float currentTime)
        {
            if (_transport == null)
            {
                return;
            }

            _lastUpdateTime = currentTime;
            _transport.Update();
            _dispatcher.ProcessEvents(_transport);
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
                    { Reason = DisconnectReason.Graceful };
                Send(msg, PipelineId.ReliableSequenced);
                _stateMachine.Transition(ConnectionState.Disconnecting, 0f);
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

        // --- Event handlers ---

        private void OnConnected(ConnectionId connectionId)
        {
            _serverConnectionId = connectionId;
            _stateMachine.Transition(ConnectionState.Handshaking, 0f);

            // Send handshake request immediately
            HandshakeRequestMessage request = new()
            {
                ProtocolVersion = NetworkConstants.ProtocolVersion,
                ContentHash = _contentHash,
                PlayerName = _playerName,
            };

            Send(request, PipelineId.ReliableSequenced);
            _logger.LogDebug("Connected to server, sending handshake");
        }

        private void OnDisconnected(ConnectionId connectionId)
        {
            _logger.LogInfo("Disconnected from server");
            _stateMachine.Transition(ConnectionState.Disconnected, 0f);
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
                _stateMachine.Transition(ConnectionState.Disconnected, 0f);
                CleanUp();
                return;
            }

            LocalPlayerId = response.PlayerId;
            ServerTickAtHandshake = response.ServerTick;
            WorldSeed = response.WorldSeed;

            _stateMachine.Transition(ConnectionState.Loading, 0f);
            _logger.LogInfo(
                $"Handshake accepted: playerId={response.PlayerId}, " +
                $"serverTick={response.ServerTick}, seed={response.WorldSeed}");
        }

        private void OnPong(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            PongMessage pong = PongMessage.Deserialize(data, offset, length);
            RoundTripTime = _lastUpdateTime - pong.EchoTimestamp;
        }

        private void OnDisconnectMessage(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            DisconnectMessage msg = DisconnectMessage.Deserialize(data, offset, length);
            _logger.LogInfo($"Server disconnected us: {msg.Reason}");
            _stateMachine.Transition(ConnectionState.Disconnected, 0f);
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
            if (_stateMachine.Current != ConnectionState.Playing)
            {
                return;
            }

            if (currentTime - _lastPingTime >= NetworkConstants.PingIntervalSeconds)
            {
                _lastPingTime = currentTime;
                PingMessage ping = new()
                    { Timestamp = currentTime };
                Send(ping, PipelineId.UnreliableSequenced);
            }
        }

        private void CleanUp()
        {
            _sendQueue?.Clear();
            _transport?.Dispose();
            _transport = null;
        }
    }
}
