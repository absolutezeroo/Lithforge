using System;
using System.Buffers;
using System.Collections.Generic;

using Lithforge.Core.Logging;
using Lithforge.Network.Connection;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using Lithforge.Network.SendQueue;
using Lithforge.Network.Transport;
using Lithforge.Voxel.Storage;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Production network server implementation.
    ///     Owns the transport, peer registry, message dispatcher, and handshake protocol.
    /// </summary>
    public sealed class NetworkServer : INetworkServer, INetworkMetricsSource
    {
        /// <summary>Logger instance for diagnostic messages.</summary>
        private readonly ILogger _logger;

        /// <summary>Maximum number of concurrent connections the server accepts.</summary>
        private readonly int _maxConnections;

        /// <summary>Scratch list of connection IDs to disconnect during timeout sweep.</summary>
        private readonly List<ConnectionId> _timeoutDisconnectList = new();

        /// <summary>Cached current wall-clock time, updated each Update call.</summary>
        private float _currentTime;

        /// <summary>Whether this server has been disposed.</summary>
        private bool _disposed;

        /// <summary>Bytes received from the network since the last metrics reset.</summary>
        private int _metricsBytesReceived;

        /// <summary>Bytes sent over the network since the last metrics reset.</summary>
        private int _metricsBytesSent;

        /// <summary>Network messages received since the last metrics reset.</summary>
        private int _metricsMessagesReceived;

        /// <summary>Network messages sent since the last metrics reset.</summary>
        private int _metricsMessagesSent;

        /// <summary>Registry of all connected peers indexed by connection ID and player ID.</summary>
        private PeerRegistry _peerRegistry;

        /// <summary>Retry queue for failed reliable sends with exponential backoff.</summary>
        private ReliableSendQueue _sendQueue;

        /// <summary>The underlying network transport (UTP driver or direct transport).</summary>
        private INetworkTransport _transport;

        /// <summary>Optional admin store for ban/op/whitelist checks during handshake.</summary>
        private AdminStore _adminStore;

        /// <summary>Optional chat command processor for handling chat messages and admin commands.</summary>
        private ChatCommandProcessor _chatProcessor;

        /// <summary>Optional player data store for loading saved state during handshake.</summary>
        private PlayerDataStore _playerDataStore;

        /// <summary>Callback invoked when a peer completes the handshake and is accepted.</summary>
        public Action<PeerInfo> OnPeerAccepted;

        /// <summary>
        ///     Callback fired when a peer is about to be removed (disconnect or timeout).
        ///     Fires BEFORE the peer is removed from the registry. Use for persistence.
        /// </summary>
        public Action<PeerInfo> OnPeerRemoved;

        /// <summary>Creates a new NetworkServer with the given logger, content hash, and connection limit.</summary>
        public NetworkServer(ILogger logger, ContentHash contentHash, int maxConnections)
        {
            _logger = logger;
            ServerContentHash = contentHash;
            _maxConnections = maxConnections;
        }

        /// <summary>
        ///     Returns a read-only list of all connected peers. Used by ServerGameLoop
        ///     for broadcast iteration.
        /// </summary>
        public IReadOnlyList<PeerInfo> AllPeers
        {
            get { return _peerRegistry.AllPeers; }
        }

        /// <summary>Number of currently connected peers.</summary>
        public int PeerCount
        {
            get { return _peerRegistry?.Count ?? 0; }
        }

        /// <summary>The content hash used to validate connecting clients.</summary>
        public ContentHash ServerContentHash { get; }

        /// <summary>The message dispatcher for routing incoming messages to handlers.</summary>
        public MessageDispatcher Dispatcher { get; private set; }

        /// <summary>The current server tick number, set by ServerGameLoop each tick.</summary>
        public uint CurrentTick { get; set; }

        /// <summary>The world seed, sent to clients during the handshake response.</summary>
        public ulong WorldSeed { get; set; }

        /// <summary>Injects the player data store and admin store for handshake integration.</summary>
        public void SetPlayerDataStore(PlayerDataStore playerDataStore, AdminStore adminStore)
        {
            _playerDataStore = playerDataStore;
            _adminStore = adminStore;
        }

        /// <summary>Injects the chat command processor for handling chat messages.</summary>
        public void SetChatProcessor(ChatCommandProcessor chatProcessor)
        {
            _chatProcessor = chatProcessor;
        }

        /// <summary>Starts the server on the given UDP port using a NetworkDriverWrapper transport.</summary>
        public bool Start(ushort port)
        {
            INetworkTransport transport = new NetworkDriverWrapper(_logger);

            bool success = transport.Listen(port);

            if (!success)
            {
                _logger.LogError($"NetworkServer failed to start on port {port}");

                transport.Dispose();

                return false;
            }

            InitCommon(transport);

            _logger.LogInfo($"NetworkServer started on port {port}, max connections: {_maxConnections}");

            return true;
        }

        /// <summary>Starts the server using an externally provided transport (e.g. DirectTransport for SP/Host).</summary>
        public bool StartWithTransport(INetworkTransport transport)
        {
            InitCommon(transport);

            _logger.LogInfo($"NetworkServer started with external transport, max connections: {_maxConnections}");

            return true;
        }

        /// <summary>
        ///     Pumps the transport, dispatches received messages, checks timeouts,
        ///     schedules pings, and flushes the reliable send queue.
        /// </summary>
        public void Update(float currentTime)
        {
            if (_transport == null)
            {
                return;
            }

            _currentTime = currentTime;

            _transport.Update();
            Dispatcher.ProcessEvents(_transport);
            CheckTimeouts(currentTime);
            SchedulePings(currentTime);
            _sendQueue.Flush(_transport, currentTime);
        }

        /// <summary>Serializes and sends a message to a specific peer, queuing for retry on failure.</summary>
        public void SendTo(ConnectionId connectionId, INetworkMessage message, int pipelineId)
        {
            int totalBytes = MessageSerializer.WriteMessage(message, out byte[] buffer);
            bool success = _transport.Send(connectionId, pipelineId, buffer, 0, totalBytes);

            if (!success)
            {
                _sendQueue.Enqueue(connectionId, pipelineId, buffer, 0, totalBytes);
            }

            _metricsBytesSent += totalBytes;
            _metricsMessagesSent++;
        }

        /// <summary>Serializes a message once and sends it to all peers in Playing state.</summary>
        public void Broadcast(INetworkMessage message, int pipelineId)
        {
            int totalBytes = MessageSerializer.WriteMessage(message, out byte[] buffer);

            // Rent from pool — send loop is synchronous, safe to return after
            byte[] sendData = ArrayPool<byte>.Shared.Rent(totalBytes);
            Array.Copy(buffer, 0, sendData, 0, totalBytes);

            IReadOnlyList<PeerInfo> peers = _peerRegistry.AllPeers;

            for (int i = 0; i < peers.Count; i++)
            {
                PeerInfo peer = peers[i];

                if (peer.StateMachine.Current != ConnectionState.Playing)
                {
                    continue;
                }

                bool success = _transport.Send(peer.ConnectionId, pipelineId, sendData, 0, totalBytes);

                if (!success)
                {
                    _sendQueue.Enqueue(peer.ConnectionId, pipelineId, sendData, 0, totalBytes);
                }
            }

            ArrayPool<byte>.Shared.Return(sendData);
        }

        /// <summary>Broadcasts a message to all Playing peers except the specified connection.</summary>
        public void BroadcastExcept(ConnectionId excludeId, INetworkMessage message, int pipelineId)
        {
            int totalBytes = MessageSerializer.WriteMessage(message, out byte[] buffer);

            // Rent from pool — send loop is synchronous, safe to return after
            byte[] sendData = ArrayPool<byte>.Shared.Rent(totalBytes);
            Array.Copy(buffer, 0, sendData, 0, totalBytes);

            IReadOnlyList<PeerInfo> peers = _peerRegistry.AllPeers;

            for (int i = 0; i < peers.Count; i++)
            {
                PeerInfo peer = peers[i];

                if (peer.StateMachine.Current != ConnectionState.Playing)
                {
                    continue;
                }

                if (peer.ConnectionId == excludeId)
                {
                    continue;
                }

                bool success = _transport.Send(peer.ConnectionId, pipelineId, sendData, 0, totalBytes);

                if (!success)
                {
                    _sendQueue.Enqueue(peer.ConnectionId, pipelineId, sendData, 0, totalBytes);
                }
            }

            ArrayPool<byte>.Shared.Return(sendData);
        }

        /// <summary>Sends a disconnect message to the peer, removes it from the registry, and closes the connection.</summary>
        public void DisconnectPeer(ConnectionId connectionId, DisconnectReason reason)
        {
            PeerInfo peer = _peerRegistry.GetByConnection(connectionId);

            if (peer == null)
            {
                return;
            }

            OnPeerRemoved?.Invoke(peer);

            DisconnectMessage msg = new()
            {
                Reason = reason,
            };
            SendTo(connectionId, msg, PipelineId.ReliableSequenced);

            peer.StateMachine.Transition(ConnectionState.Disconnecting, _currentTime);
            _transport.Disconnect(connectionId);
            _sendQueue.RemoveForConnection(connectionId);
            _peerRegistry.Remove(connectionId);

            _logger.LogInfo(
                $"Disconnected peer {connectionId} (player {peer.AssignedPlayerId}): {reason}");
        }

        /// <summary>Returns the player ID assigned to the given connection, or 0 if not found.</summary>
        public ushort GetPlayerId(ConnectionId connectionId)
        {
            PeerInfo peer = _peerRegistry.GetByConnection(connectionId);
            return peer?.AssignedPlayerId ?? 0;
        }

        /// <summary>Disconnects all peers with ServerShutdown reason, clears the send queue, and disposes the transport.</summary>
        public void Shutdown()
        {
            if (_peerRegistry != null)
            {
                IReadOnlyList<PeerInfo> peers = _peerRegistry.AllPeers;

                // Iterate in reverse since DisconnectPeer modifies the collection
                for (int i = peers.Count - 1; i >= 0; i--)
                {
                    DisconnectPeer(peers[i].ConnectionId, DisconnectReason.ServerShutdown);
                }
            }

            _sendQueue?.Clear();
            _transport?.Dispose();
            _transport = null;
            _logger.LogInfo("NetworkServer shut down");
        }

        /// <summary>Disposes this server, calling Shutdown if not already disposed.</summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Shutdown();
            }
        }

        /// <summary>
        ///     Shared initialization for both Start and StartWithTransport. Sets up the transport,
        ///     dispatcher, peer registry, send queue, and core message handlers.
        /// </summary>
        private void InitCommon(INetworkTransport transport)
        {
            _transport = transport;
            Dispatcher = new MessageDispatcher(_logger);
            _peerRegistry = new PeerRegistry();
            _sendQueue = new ReliableSendQueue(_logger);

            Dispatcher.OnConnect(OnPeerConnected);
            Dispatcher.OnDisconnect(OnPeerDisconnected);
            Dispatcher.OnDataReceived(OnDataReceivedMetrics);
            Dispatcher.RegisterHandler(MessageType.HandshakeRequest, OnHandshakeRequest);
            Dispatcher.RegisterHandler(MessageType.Ping, OnPing);
            Dispatcher.RegisterHandler(MessageType.Pong, OnPong);
            Dispatcher.RegisterHandler(MessageType.Disconnect, OnDisconnectMessage);
            Dispatcher.RegisterHandler(MessageType.ChatCmd, OnChatCmd);
        }

        /// <summary>
        ///     Returns the PeerInfo for the given connection, or null if not found.
        ///     Used by ServerGameLoop to access per-player InterestState.
        /// </summary>
        public PeerInfo GetPeer(ConnectionId connectionId)
        {
            return _peerRegistry.GetByConnection(connectionId);
        }

        /// <summary>
        ///     Returns the PeerInfo for the given player ID, or null if not found.
        /// </summary>
        public PeerInfo GetPeerByPlayerId(ushort playerId)
        {
            return _peerRegistry.GetByPlayerId(playerId);
        }

        /// <summary>
        ///     Resets the idle timeout for the given peer. Call on every received message.
        /// </summary>
        public void TouchPeer(ConnectionId connectionId)
        {
            PeerInfo peer = _peerRegistry.GetByConnection(connectionId);

            if (peer != null)
            {
                peer.LastMessageTime = _currentTime;
            }
        }

        // --- Event handlers ---

        /// <summary>Handles a new transport connection: registers the peer or rejects if full.</summary>
        private void OnPeerConnected(ConnectionId connectionId)
        {
            if (_peerRegistry.Count >= _maxConnections)
            {
                _logger.LogWarning(
                    $"Rejecting connection {connectionId}: server full ({_peerRegistry.Count}/{_maxConnections})");
                _transport.Disconnect(connectionId);
                return;
            }

            PeerInfo peer = _peerRegistry.Add(connectionId);
            peer.LastMessageTime = _currentTime;
            peer.StateMachine.Transition(ConnectionState.Connecting, _currentTime);
            peer.StateMachine.Transition(ConnectionState.Handshaking, _currentTime);

            _logger.LogDebug($"Peer connected: {connectionId}, awaiting handshake");
        }

        /// <summary>Handles a transport disconnect: fires OnPeerRemoved, then removes the peer from the registry and send queue.</summary>
        private void OnPeerDisconnected(ConnectionId connectionId)
        {
            PeerInfo peer = _peerRegistry.GetByConnection(connectionId);

            if (peer == null)
            {
                return;
            }

            OnPeerRemoved?.Invoke(peer);
            _sendQueue.RemoveForConnection(connectionId);
            _peerRegistry.Remove(connectionId);

            _logger.LogInfo(
                $"Peer disconnected: {connectionId} (player {peer.AssignedPlayerId})");
        }

        /// <summary>
        ///     Validates protocol version and content hash from the handshake request,
        ///     assigns a player ID on success, and transitions through the connection states.
        /// </summary>
        private void OnHandshakeRequest(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            TouchPeer(connectionId);
            PeerInfo peer = _peerRegistry.GetByConnection(connectionId);

            if (peer == null || peer.StateMachine.Current != ConnectionState.Handshaking)
            {
                _logger.LogWarning(
                    $"Received handshake from {connectionId} in unexpected state");
                return;
            }

            HandshakeRequestMessage request = HandshakeRequestMessage.Deserialize(data, offset, length);

            // Validate protocol version
            if (request.ProtocolVersion != NetworkConstants.ProtocolVersion)
            {
                SendHandshakeReject(connectionId, HandshakeRejectReason.ProtocolMismatch);
                _logger.LogWarning(
                    $"Rejected {connectionId}: protocol mismatch " +
                    $"(client={request.ProtocolVersion}, server={NetworkConstants.ProtocolVersion})");
                return;
            }

            // Validate content hash
            if (request.ContentHash != ServerContentHash)
            {
                SendHandshakeReject(connectionId, HandshakeRejectReason.ContentMismatch);
                _logger.LogWarning(
                    $"Rejected {connectionId}: content mismatch " +
                    $"(client={request.ContentHash}, server={ServerContentHash})");
                return;
            }

            // Extract UUID and public key
            string playerUuid = request.PlayerUuid ?? "";
            peer.PlayerUuid = playerUuid;

            // Check bans
            if (_adminStore is not null && !string.IsNullOrEmpty(playerUuid))
            {
                if (_adminStore.IsBanned(playerUuid, ""))
                {
                    SendHandshakeReject(connectionId, HandshakeRejectReason.Banned);
                    _logger.LogWarning($"Rejected {connectionId}: player {playerUuid} is banned");
                    return;
                }

                if (_adminStore.WhitelistEnabled && !_adminStore.IsWhitelisted(playerUuid))
                {
                    SendHandshakeReject(connectionId, HandshakeRejectReason.Banned);
                    _logger.LogWarning(
                        $"Rejected {connectionId}: player {playerUuid} not on whitelist");
                    return;
                }

                peer.IsAdmin = _adminStore.IsOp(playerUuid);
            }

            // Load player data if available
            if (_playerDataStore is not null && !string.IsNullOrEmpty(playerUuid))
            {
                peer.PlayerData = _playerDataStore.Load(playerUuid);
            }

            // Accept
            ushort playerId = _peerRegistry.AllocatePlayerId(connectionId);
            peer.PlayerName = request.PlayerName ?? "";

            HandshakeResponseMessage response = new()
            {
                Accepted = true,
                RejectReason = HandshakeRejectReason.None,
                PlayerId = playerId,
                ServerTick = CurrentTick,
                WorldSeed = WorldSeed,
            };

            SendTo(connectionId, response, PipelineId.ReliableSequenced);

            // Auto-transition through Authenticating → Configuring → Loading.
            // Future implementations can pause at Authenticating (external auth)
            // or Configuring (registry sync, mod negotiation) before proceeding.
            peer.StateMachine.Transition(ConnectionState.Authenticating, _currentTime);
            peer.StateMachine.Transition(ConnectionState.Configuring, _currentTime);
            peer.StateMachine.Transition(ConnectionState.Loading, _currentTime);

            _logger.LogInfo(
                $"Accepted peer {connectionId} as player {playerId} ({peer.PlayerName}, uuid={playerUuid})");

            OnPeerAccepted?.Invoke(peer);
        }

        /// <summary>Handles a Pong response from a client, computing per-peer round-trip time.</summary>
        private void OnPong(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            TouchPeer(connectionId);
            PongMessage pong = PongMessage.Deserialize(data, offset, length);
            PeerInfo peer = _peerRegistry.GetByConnection(connectionId);

            if (peer is not null)
            {
                peer.RoundTripTime = _currentTime - pong.EchoTimestamp;
            }
        }

        /// <summary>Handles a Ping message by echoing the timestamp back in a Pong response.</summary>
        private void OnPing(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            TouchPeer(connectionId);
            PingMessage ping = PingMessage.Deserialize(data, offset, length);
            PongMessage pong = new()
            {
                EchoTimestamp = ping.Timestamp, ServerTick = CurrentTick,
            };
            SendTo(connectionId, pong, PipelineId.UnreliableSequenced);
        }

        /// <summary>Handles a graceful Disconnect message from a client, removing the peer.</summary>
        private void OnDisconnectMessage(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            TouchPeer(connectionId);
            DisconnectMessage msg = DisconnectMessage.Deserialize(data, offset, length);
            _logger.LogInfo($"Peer {connectionId} requested disconnect: {msg.Reason}");

            PeerInfo peer = _peerRegistry.GetByConnection(connectionId);

            if (peer is not null)
            {
                OnPeerRemoved?.Invoke(peer);
            }

            _sendQueue.RemoveForConnection(connectionId);
            _transport.Disconnect(connectionId);
            _peerRegistry.Remove(connectionId);
        }

        /// <summary>Handles a chat command message from a client, delegating to the chat processor.</summary>
        private void OnChatCmd(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            TouchPeer(connectionId);
            PeerInfo peer = _peerRegistry.GetByConnection(connectionId);

            if (peer == null || peer.StateMachine.Current != ConnectionState.Playing)
            {
                return;
            }

            if (_chatProcessor == null)
            {
                return;
            }

            ChatCmdMessage msg = ChatCmdMessage.Deserialize(data, offset, length);
            _chatProcessor.ProcessChat(peer, msg.Content);
        }

        /// <summary>Sends a rejection response and immediately disconnects the peer.</summary>
        private void SendHandshakeReject(ConnectionId connectionId, HandshakeRejectReason reason)
        {
            HandshakeResponseMessage response = new()
            {
                Accepted = false,
                RejectReason = reason,
                PlayerId = 0,
                ServerTick = 0,
                WorldSeed = 0,
            };

            SendTo(connectionId, response, PipelineId.ReliableSequenced);

            // Schedule disconnect after sending rejection
            _transport.Disconnect(connectionId);
            _peerRegistry.Remove(connectionId);
        }

        /// <summary>
        ///     Scans all peers for handshake, loading, and idle timeouts, disconnecting
        ///     any that have exceeded their threshold.
        /// </summary>
        private void CheckTimeouts(float currentTime)
        {
            _timeoutDisconnectList.Clear();
            IReadOnlyList<PeerInfo> peers = _peerRegistry.AllPeers;

            for (int i = 0; i < peers.Count; i++)
            {
                PeerInfo peer = peers[i];
                ConnectionState state = peer.StateMachine.Current;

                if (state is ConnectionState.Handshaking
                    or ConnectionState.Authenticating
                    or ConnectionState.Configuring)
                {
                    if (peer.StateMachine.IsTimedOut(currentTime, NetworkConstants.HandshakeTimeoutSeconds))
                    {
                        _timeoutDisconnectList.Add(peer.ConnectionId);
                    }
                }
                else if (state == ConnectionState.Loading)
                {
                    if (currentTime - peer.LastMessageTime > NetworkConstants.LoadingTimeoutSeconds)
                    {
                        _timeoutDisconnectList.Add(peer.ConnectionId);
                    }
                }
                else if (state == ConnectionState.Playing)
                {
                    if (currentTime - peer.LastMessageTime > NetworkConstants.IdleTimeoutSeconds)
                    {
                        _timeoutDisconnectList.Add(peer.ConnectionId);
                    }
                }
            }

            for (int i = 0; i < _timeoutDisconnectList.Count; i++)
            {
                _logger.LogWarning($"Peer {_timeoutDisconnectList[i]} timed out");
                DisconnectPeer(_timeoutDisconnectList[i], DisconnectReason.Timeout);
            }
        }

        /// <summary>Sends periodic Ping messages to all Playing peers for keepalive and RTT measurement.</summary>
        private void SchedulePings(float currentTime)
        {
            IReadOnlyList<PeerInfo> peers = _peerRegistry.AllPeers;

            for (int i = 0; i < peers.Count; i++)
            {
                PeerInfo peer = peers[i];

                if (peer.StateMachine.Current != ConnectionState.Playing)
                {
                    continue;
                }

                if (currentTime - peer.LastPingTime >= NetworkConstants.PingIntervalSeconds)
                {
                    peer.LastPingTime = currentTime;
                    PingMessage ping = new()
                    {
                        Timestamp = currentTime,
                    };
                    SendTo(peer.ConnectionId, ping, PipelineId.UnreliableSequenced);
                }
            }
        }

        /// <summary>Callback for tracking received bytes and message counts for metrics.</summary>
        private void OnDataReceivedMetrics(int byteCount)
        {
            _metricsBytesReceived += byteCount;
            _metricsMessagesReceived++;
        }

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
            get { return PeerCount; }
        }

        /// <inheritdoc />
        float INetworkMetricsSource.AveragePingMs
        {
            get
            {
                if (_peerRegistry == null)
                {
                    return 0f;
                }

                IReadOnlyList<PeerInfo> peers = _peerRegistry.AllPeers;
                int count = 0;
                float totalRtt = 0f;

                for (int i = 0; i < peers.Count; i++)
                {
                    if (peers[i].RoundTripTime > 0f)
                    {
                        totalRtt += peers[i].RoundTripTime;
                        count++;
                    }
                }

                return count > 0 ? totalRtt / count * 1000f : 0f;
            }
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
