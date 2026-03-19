using System;
using System.Collections.Generic;

using Lithforge.Core.Logging;
using Lithforge.Network.Connection;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using Lithforge.Network.SendQueue;
using Lithforge.Network.Transport;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Production network server implementation.
    ///     Owns the transport, peer registry, message dispatcher, and handshake protocol.
    /// </summary>
    public sealed class NetworkServer : INetworkServer
    {
        private readonly ILogger _logger;

        private readonly int _maxConnections;

        private readonly List<ConnectionId> _timeoutDisconnectList = new();

        private float _currentTime;

        private bool _disposed;

        private PeerRegistry _peerRegistry;

        private ReliableSendQueue _sendQueue;

        private INetworkTransport _transport;

        public Action<PeerInfo> OnPeerAccepted;

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

        public int PeerCount
        {
            get { return _peerRegistry?.Count ?? 0; }
        }

        public ContentHash ServerContentHash { get; }

        public MessageDispatcher Dispatcher { get; private set; }

        public uint CurrentTick { get; set; }

        public ulong WorldSeed { get; set; }

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

        public bool StartWithTransport(INetworkTransport transport)
        {
            InitCommon(transport);

            _logger.LogInfo($"NetworkServer started with external transport, max connections: {_maxConnections}");

            return true;
        }

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

        public void SendTo(ConnectionId connectionId, INetworkMessage message, int pipelineId)
        {
            int totalBytes = MessageSerializer.WriteMessage(message, out byte[] buffer);
            bool success = _transport.Send(connectionId, pipelineId, buffer, 0, totalBytes);

            if (!success)
            {
                _sendQueue.Enqueue(connectionId, pipelineId, buffer, 0, totalBytes);
            }
        }

        public void Broadcast(INetworkMessage message, int pipelineId)
        {
            int totalBytes = MessageSerializer.WriteMessage(message, out byte[] buffer);

            // Copy to dedicated buffer since Send may be async
            byte[] sendData = new byte[totalBytes];
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
        }

        public void BroadcastExcept(ConnectionId excludeId, INetworkMessage message, int pipelineId)
        {
            int totalBytes = MessageSerializer.WriteMessage(message, out byte[] buffer);

            byte[] sendData = new byte[totalBytes];
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
        }

        public void DisconnectPeer(ConnectionId connectionId, DisconnectReason reason)
        {
            PeerInfo peer = _peerRegistry.GetByConnection(connectionId);

            if (peer == null)
            {
                return;
            }

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

        public ushort GetPlayerId(ConnectionId connectionId)
        {
            PeerInfo peer = _peerRegistry.GetByConnection(connectionId);
            return peer?.AssignedPlayerId ?? 0;
        }

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

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Shutdown();
            }
        }

        private void InitCommon(INetworkTransport transport)
        {
            _transport = transport;
            Dispatcher = new MessageDispatcher(_logger);
            _peerRegistry = new PeerRegistry();
            _sendQueue = new ReliableSendQueue(_logger);

            Dispatcher.OnConnect(OnPeerConnected);
            Dispatcher.OnDisconnect(OnPeerDisconnected);
            Dispatcher.RegisterHandler(MessageType.HandshakeRequest, OnHandshakeRequest);
            Dispatcher.RegisterHandler(MessageType.Ping, OnPing);
            Dispatcher.RegisterHandler(MessageType.Disconnect, OnDisconnectMessage);
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

        private void OnPeerDisconnected(ConnectionId connectionId)
        {
            PeerInfo peer = _peerRegistry.GetByConnection(connectionId);

            if (peer == null)
            {
                return;
            }

            _sendQueue.RemoveForConnection(connectionId);
            _peerRegistry.Remove(connectionId);

            _logger.LogInfo(
                $"Peer disconnected: {connectionId} (player {peer.AssignedPlayerId})");
        }

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
            peer.StateMachine.Transition(ConnectionState.Loading, _currentTime);

            _logger.LogInfo(
                $"Accepted peer {connectionId} as player {playerId} ({peer.PlayerName})");

            OnPeerAccepted?.Invoke(peer);
        }

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

        private void OnDisconnectMessage(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            TouchPeer(connectionId);
            DisconnectMessage msg = DisconnectMessage.Deserialize(data, offset, length);
            _logger.LogInfo($"Peer {connectionId} requested disconnect: {msg.Reason}");

            _sendQueue.RemoveForConnection(connectionId);
            _transport.Disconnect(connectionId);
            _peerRegistry.Remove(connectionId);
        }

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

        private void CheckTimeouts(float currentTime)
        {
            _timeoutDisconnectList.Clear();
            IReadOnlyList<PeerInfo> peers = _peerRegistry.AllPeers;

            for (int i = 0; i < peers.Count; i++)
            {
                PeerInfo peer = peers[i];
                ConnectionState state = peer.StateMachine.Current;

                if (state == ConnectionState.Handshaking)
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
    }
}
