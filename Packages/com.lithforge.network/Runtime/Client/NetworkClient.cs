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
    public sealed class NetworkClient : INetworkClient, INetworkMetricsSource
    {
        /// <summary>Content hash for validating compatibility with the server during handshake.</summary>
        private readonly ContentHash _contentHash;

        /// <summary>Logger instance for diagnostic messages.</summary>
        private readonly ILogger _logger;

        /// <summary>Player name sent to the server during the handshake request.</summary>
        private readonly string _playerName;

        /// <summary>Whether this client has been disposed.</summary>
        private bool _disposed;

        /// <summary>Wall-clock time of the last ping sent, for scheduling periodic pings.</summary>
        private float _lastPingTime;

        /// <summary>Cached current wall-clock time, updated each Update call.</summary>
        private float _lastUpdateTime;

        /// <summary>Bytes received from the network since the last metrics reset.</summary>
        private int _metricsBytesReceived;

        /// <summary>Bytes sent over the network since the last metrics reset.</summary>
        private int _metricsBytesSent;

        /// <summary>Network messages received since the last metrics reset.</summary>
        private int _metricsMessagesReceived;

        /// <summary>Network messages sent since the last metrics reset.</summary>
        private int _metricsMessagesSent;

        /// <summary>True if this client created the transport and is responsible for disposing it.</summary>
        private bool _ownsTransport;

        /// <summary>Retry queue for failed reliable sends with exponential backoff.</summary>
        private ReliableSendQueue _sendQueue;

        /// <summary>Connection ID of the server endpoint.</summary>
        private ConnectionId _serverConnectionId;

        /// <summary>Connection state machine tracking the client's lifecycle.</summary>
        private ConnectionStateMachine _stateMachine;

        /// <summary>The underlying network transport (UTP driver or direct transport).</summary>
        private INetworkTransport _transport;

        /// <summary>Creates a new NetworkClient with the given logger, content hash, and player name.</summary>
        public NetworkClient(ILogger logger, ContentHash contentHash, string playerName)
        {
            _logger = logger;
            _contentHash = contentHash;
            _playerName = playerName ?? "";
        }

        /// <summary>The current connection state of this client.</summary>
        public ConnectionState State
        {
            get
            {
                return _stateMachine?.Current ?? ConnectionState.Disconnected;
            }
        }

        /// <summary>The player ID assigned by the server during the handshake.</summary>
        public ushort LocalPlayerId { get; private set; }

        /// <summary>The server tick at the time of handshake acceptance, for tick synchronization.</summary>
        public uint ServerTickAtHandshake { get; private set; }

        /// <summary>The world seed received from the server during the handshake.</summary>
        public ulong WorldSeed { get; private set; }

        /// <summary>The most recently measured round-trip time to the server in seconds.</summary>
        public float RoundTripTime { get; private set; }

        /// <summary>The message dispatcher for routing incoming server messages to handlers.</summary>
        public MessageDispatcher Dispatcher { get; private set; }

        /// <summary>Callback invoked when the handshake completes successfully.</summary>
        public Action OnHandshakeComplete { get; set; }

        /// <summary>True if the client is in the Playing connection state.</summary>
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

        /// <summary>True if the client is in any connected state (not Disconnected or Disconnecting).</summary>
        public bool IsConnected
        {
            get
            {
                return _stateMachine?.Current is ConnectionState.Handshaking
                    or ConnectionState.Authenticating
                    or ConnectionState.Configuring
                    or ConnectionState.Loading
                    or ConnectionState.Playing
                    or ConnectionState.Reconnecting;
            }
        }

        /// <summary>Connects to a remote server at the given address and port using a UTP transport.</summary>
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
            Dispatcher.OnDataReceived(OnDataReceivedMetrics);
            Dispatcher.RegisterHandler(MessageType.HandshakeResponse, OnHandshakeResponse);
            Dispatcher.RegisterHandler(MessageType.Pong, OnPong);
            Dispatcher.RegisterHandler(MessageType.Ping, OnServerPing);
            Dispatcher.RegisterHandler(MessageType.Disconnect, OnDisconnectMessage);

            _stateMachine.Transition(ConnectionState.Connecting, _lastUpdateTime);
            _serverConnectionId = _transport.Connect(address, port);

            _logger.LogInfo($"Connecting to {address}:{port}");
        }

        /// <summary>Connects using an externally provided transport (e.g. DirectTransport for SP/Host).</summary>
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
            Dispatcher.OnDataReceived(OnDataReceivedMetrics);
            Dispatcher.RegisterHandler(MessageType.HandshakeResponse, OnHandshakeResponse);
            Dispatcher.RegisterHandler(MessageType.Pong, OnPong);
            Dispatcher.RegisterHandler(MessageType.Ping, OnServerPing);
            Dispatcher.RegisterHandler(MessageType.Disconnect, OnDisconnectMessage);

            _stateMachine.Transition(ConnectionState.Connecting, _lastUpdateTime);
            // DirectTransportClient.Connect() returns the fixed server connection ID
            _serverConnectionId = _transport.Connect("localhost", 0);

            _logger.LogInfo("Connecting via direct transport");
        }

        /// <summary>
        ///     Pumps the transport, dispatches received messages, checks handshake timeout,
        ///     schedules pings, and flushes the reliable send queue.
        /// </summary>
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
            _sendQueue.Flush(_transport, currentTime);
        }

        /// <summary>Serializes and sends a message to the server, queuing for retry on failure.</summary>
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

            _metricsBytesSent += totalBytes;
            _metricsMessagesSent++;
        }

        /// <summary>Sends a graceful disconnect message to the server and cleans up resources.</summary>
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

        /// <summary>Disposes this client, cleaning up the transport if not already disposed.</summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                CleanUp();
            }
        }

        /// <summary>Handles the transport Connect event by sending the handshake request.</summary>
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

        /// <summary>Handles the transport Disconnect event, transitioning to Disconnected and cleaning up.</summary>
        private void OnDisconnected(ConnectionId connectionId)
        {
            _logger.LogInfo("Disconnected from server");

            _stateMachine.Transition(ConnectionState.Disconnected, _lastUpdateTime);

            CleanUp();
        }

        /// <summary>
        ///     Processes the handshake response from the server. On acceptance, stores
        ///     player ID, tick, and seed, then transitions to Loading state.
        /// </summary>
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

            // Auto-transition through Authenticating → Configuring → Loading.
            // Mirrors the server-side auto-transition chain.
            _stateMachine.Transition(ConnectionState.Authenticating, _lastUpdateTime);
            _stateMachine.Transition(ConnectionState.Configuring, _lastUpdateTime);
            _stateMachine.Transition(ConnectionState.Loading, _lastUpdateTime);
            _logger.LogInfo(
                $"Handshake accepted: playerId={response.PlayerId}, " +
                $"serverTick={response.ServerTick}, seed={response.WorldSeed}");

            OnHandshakeComplete?.Invoke();
        }

        /// <summary>Handles a Pong response, computing the round-trip time from the echoed timestamp.</summary>
        private void OnPong(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            PongMessage pong = PongMessage.Deserialize(data, offset, length);

            RoundTripTime = _lastUpdateTime - pong.EchoTimestamp;
        }

        /// <summary>Handles a Ping from the server by echoing back a Pong with the original timestamp.</summary>
        private void OnServerPing(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            PingMessage ping = PingMessage.Deserialize(data, offset, length);
            PongMessage pong = new()
            {
                EchoTimestamp = ping.Timestamp, ServerTick = 0,
            };

            Send(pong, PipelineId.UnreliableSequenced);
        }

        /// <summary>Handles a server-initiated Disconnect message, transitioning to Disconnected and cleaning up.</summary>
        private void OnDisconnectMessage(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            DisconnectMessage msg = DisconnectMessage.Deserialize(data, offset, length);

            _logger.LogInfo($"Server disconnected us: {msg.Reason}");

            _stateMachine.Transition(ConnectionState.Disconnected, _lastUpdateTime);

            CleanUp();
        }

        /// <summary>Checks for handshake timeout during connecting/handshaking states.</summary>
        private void CheckTimeout(float currentTime)
        {
            ConnectionState state = _stateMachine.Current;

            if (state is ConnectionState.Connecting
                or ConnectionState.Handshaking
                or ConnectionState.Authenticating
                or ConnectionState.Configuring)
            {
                if (_stateMachine.IsTimedOut(currentTime, NetworkConstants.HandshakeTimeoutSeconds))
                {
                    _logger.LogError("Connection timed out during handshake");

                    _stateMachine.Transition(ConnectionState.Disconnected, currentTime);

                    CleanUp();
                }
            }
        }

        /// <summary>Sends periodic Ping messages to the server for keepalive and RTT measurement.</summary>
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

        /// <summary>Clears the send queue and disposes the transport if this client owns it.</summary>
        private void CleanUp()
        {
            _sendQueue?.Clear();

            if (_ownsTransport)
            {
                _transport?.Dispose();
            }

            _transport = null;
        }

        /// <summary>Callback for tracking received bytes and message counts for metrics.</summary>
        private void OnDataReceivedMetrics(int byteCount)
        {
            _metricsBytesReceived += byteCount;
            _metricsMessagesReceived++;
        }

        // --- INetworkMetricsSource ---

        /// <inheritdoc />
        int INetworkMetricsSource.BytesSent
        {
            get { return _metricsBytesSent; }
        }

        /// <inheritdoc />
        int INetworkMetricsSource.BytesReceived
        {
            get { return _metricsBytesReceived; }
        }

        /// <inheritdoc />
        int INetworkMetricsSource.MessagesSent
        {
            get { return _metricsMessagesSent; }
        }

        /// <inheritdoc />
        int INetworkMetricsSource.MessagesReceived
        {
            get { return _metricsMessagesReceived; }
        }

        /// <inheritdoc />
        int INetworkMetricsSource.PendingReliableQueueCount
        {
            get { return _sendQueue?.Count ?? 0; }
        }

        /// <inheritdoc />
        int INetworkMetricsSource.PeerCount
        {
            get { return IsConnected ? 1 : 0; }
        }

        /// <inheritdoc />
        float INetworkMetricsSource.AveragePingMs
        {
            get { return RoundTripTime * 1000f; }
        }

        /// <inheritdoc />
        void INetworkMetricsSource.SampleAndReset()
        {
            _metricsBytesSent = 0;
            _metricsBytesReceived = 0;
            _metricsMessagesSent = 0;
            _metricsMessagesReceived = 0;
        }
    }
}
