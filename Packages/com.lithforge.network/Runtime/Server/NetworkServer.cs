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
    /// Production network server implementation.
    /// Owns the transport, peer registry, message dispatcher, and handshake protocol.
    /// </summary>
    public sealed class NetworkServer : INetworkServer
    {
        private readonly ILogger _logger;
        private readonly ContentHash _contentHash;
        private readonly int _maxConnections;

        private INetworkTransport _transport;
        private MessageDispatcher _dispatcher;
        private PeerRegistry _peerRegistry;
        private ReliableSendQueue _sendQueue;
        private bool _disposed;

        // Reusable list for timeout iteration (avoids allocation during Update)
        private readonly List<ConnectionId> _timeoutDisconnectList = new List<ConnectionId>();

        public int PeerCount
        {
            get { return _peerRegistry != null ? _peerRegistry.Count : 0; }
        }

        public ContentHash ServerContentHash
        {
            get { return _contentHash; }
        }

        public MessageDispatcher Dispatcher
        {
            get { return _dispatcher; }
        }

        public uint CurrentTick { get; set; }
        public ulong WorldSeed { get; set; }

        public NetworkServer(ILogger logger, ContentHash contentHash, int maxConnections)
        {
            _logger = logger;
            _contentHash = contentHash;
            _maxConnections = maxConnections;
        }

        public bool Start(ushort port)
        {
            _transport = new NetworkDriverWrapper(_logger);
            _dispatcher = new MessageDispatcher(_logger);
            _peerRegistry = new PeerRegistry();
            _sendQueue = new ReliableSendQueue(_logger);

            _dispatcher.OnConnect(OnPeerConnected);
            _dispatcher.OnDisconnect(OnPeerDisconnected);
            _dispatcher.RegisterHandler(MessageType.HandshakeRequest, OnHandshakeRequest);
            _dispatcher.RegisterHandler(MessageType.Ping, OnPing);
            _dispatcher.RegisterHandler(MessageType.Disconnect, OnDisconnectMessage);

            bool success = _transport.Listen(port);

            if (!success)
            {
                _logger.LogError($"NetworkServer failed to start on port {port}");
                return false;
            }

            _logger.LogInfo($"NetworkServer started on port {port}, max connections: {_maxConnections}");
            return true;
        }

        public void Update(float currentTime)
        {
            if (_transport == null)
            {
                return;
            }

            _transport.Update();
            _dispatcher.ProcessEvents(_transport);
            CheckTimeouts(currentTime);
            SchedulePings(currentTime);
            _sendQueue.Flush(_transport);
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

            DisconnectMessage msg = new DisconnectMessage { Reason = reason };
            SendTo(connectionId, msg, PipelineId.ReliableSequenced);

            peer.StateMachine.Transition(ConnectionState.Disconnecting, 0f);
            _transport.Disconnect(connectionId);
            _sendQueue.RemoveForConnection(connectionId);
            _peerRegistry.Remove(connectionId);

            _logger.LogInfo(
                $"Disconnected peer {connectionId} (player {peer.AssignedPlayerId}): {reason}");
        }

        public ushort GetPlayerId(ConnectionId connectionId)
        {
            PeerInfo peer = _peerRegistry.GetByConnection(connectionId);
            return peer != null ? peer.AssignedPlayerId : (ushort)0;
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
            peer.StateMachine.Transition(ConnectionState.Connecting, 0f);
            peer.StateMachine.Transition(ConnectionState.Handshaking, 0f);

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
            if (request.ContentHash != _contentHash)
            {
                SendHandshakeReject(connectionId, HandshakeRejectReason.ContentMismatch);
                _logger.LogWarning(
                    $"Rejected {connectionId}: content mismatch " +
                    $"(client={request.ContentHash}, server={_contentHash})");
                return;
            }

            // Accept
            ushort playerId = _peerRegistry.AllocatePlayerId(connectionId);
            peer.PlayerName = request.PlayerName ?? "";

            HandshakeResponseMessage response = new HandshakeResponseMessage
            {
                Accepted = true,
                RejectReason = HandshakeRejectReason.None,
                PlayerId = playerId,
                ServerTick = CurrentTick,
                WorldSeed = WorldSeed
            };

            SendTo(connectionId, response, PipelineId.ReliableSequenced);
            peer.StateMachine.Transition(ConnectionState.Loading, 0f);

            _logger.LogInfo(
                $"Accepted peer {connectionId} as player {playerId} ({peer.PlayerName})");
        }

        private void OnPing(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            PingMessage ping = PingMessage.Deserialize(data, offset, length);
            PongMessage pong = new PongMessage
            {
                EchoTimestamp = ping.Timestamp,
                ServerTick = CurrentTick
            };
            SendTo(connectionId, pong, PipelineId.UnreliableSequenced);
        }

        private void OnDisconnectMessage(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            DisconnectMessage msg = DisconnectMessage.Deserialize(data, offset, length);
            _logger.LogInfo($"Peer {connectionId} requested disconnect: {msg.Reason}");

            _sendQueue.RemoveForConnection(connectionId);
            _transport.Disconnect(connectionId);
            _peerRegistry.Remove(connectionId);
        }

        private void SendHandshakeReject(ConnectionId connectionId, HandshakeRejectReason reason)
        {
            HandshakeResponseMessage response = new HandshakeResponseMessage
            {
                Accepted = false,
                RejectReason = reason,
                PlayerId = 0,
                ServerTick = 0,
                WorldSeed = 0
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
                else if (state == ConnectionState.Playing)
                {
                    if (peer.StateMachine.IsTimedOut(currentTime, NetworkConstants.IdleTimeoutSeconds))
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
                    PingMessage ping = new PingMessage { Timestamp = currentTime };
                    SendTo(peer.ConnectionId, ping, PipelineId.UnreliableSequenced);
                }
            }
        }
    }
}
