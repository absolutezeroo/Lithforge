using System.Collections.Generic;

namespace Lithforge.Network.Connection
{
    /// <summary>
    ///     Manages connected peers indexed by both ConnectionId and PlayerId.
    ///     Allocates sequential player IDs starting at 1 (0 is reserved for host in listen-server mode).
    /// </summary>
    public sealed class PeerRegistry
    {
        /// <summary>
        /// Cached list rebuilt when peers change, avoiding allocation during broadcast iteration.
        /// </summary>
        private readonly List<PeerInfo> _allPeersCache = new();

        /// <summary>
        /// Maps ConnectionId.Value to PeerInfo for connection-based lookups.
        /// </summary>
        private readonly Dictionary<int, PeerInfo> _byConnection = new();

        /// <summary>
        /// Maps PlayerId to PeerInfo for player-based lookups.
        /// </summary>
        private readonly Dictionary<ushort, PeerInfo> _byPlayerId = new();

        /// <summary>
        /// Whether the cached peer list needs to be rebuilt.
        /// </summary>
        private bool _cacheDirty = true;

        /// <summary>
        /// Next player ID to allocate (starts at 1; 0 is reserved for host).
        /// </summary>
        private ushort _nextPlayerId = 1;

        /// <summary>
        /// Returns the number of connected peers.
        /// </summary>
        public int Count
        {
            get { return _byConnection.Count; }
        }

        /// <summary>
        ///     Returns a read-only snapshot of all connected peers.
        ///     The list is cached and rebuilt only when peers are added or removed.
        /// </summary>
        public IReadOnlyList<PeerInfo> AllPeers
        {
            get
            {
                if (_cacheDirty)
                {
                    _allPeersCache.Clear();
                    _allPeersCache.AddRange(_byConnection.Values);
                    _cacheDirty = false;
                }

                return _allPeersCache;
            }
        }

        /// <summary>
        ///     Adds a new peer for the given connection. Returns the new PeerInfo.
        /// </summary>
        public PeerInfo Add(ConnectionId connectionId)
        {
            PeerInfo peer = new(connectionId);
            _byConnection[connectionId.Value] = peer;
            _cacheDirty = true;
            return peer;
        }

        /// <summary>
        ///     Removes a peer by connection ID. Returns true if the peer was found and removed.
        /// </summary>
        public bool Remove(ConnectionId connectionId)
        {
            if (_byConnection.TryGetValue(connectionId.Value, out PeerInfo peer))
            {
                _byConnection.Remove(connectionId.Value);

                if (peer.AssignedPlayerId != 0)
                {
                    _byPlayerId.Remove(peer.AssignedPlayerId);
                }

                _cacheDirty = true;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Allocates the next available player ID and assigns it to the peer.
        ///     Returns the allocated ID.
        /// </summary>
        public ushort AllocatePlayerId(ConnectionId connectionId)
        {
            if (!_byConnection.TryGetValue(connectionId.Value, out PeerInfo peer))
            {
                return 0;
            }

            ushort playerId = _nextPlayerId;

            _nextPlayerId++;

            peer.AssignedPlayerId = playerId;
            _byPlayerId[playerId] = peer;

            return playerId;
        }

        /// <summary>
        ///     Looks up a peer by connection ID. Returns null if not found.
        /// </summary>
        public PeerInfo GetByConnection(ConnectionId connectionId)
        {
            _byConnection.TryGetValue(connectionId.Value, out PeerInfo peer);

            return peer;
        }

        /// <summary>
        ///     Looks up a peer by player ID. Returns null if not found.
        /// </summary>
        public PeerInfo GetByPlayerId(ushort playerId)
        {
            _byPlayerId.TryGetValue(playerId, out PeerInfo peer);

            return peer;
        }
    }
}
