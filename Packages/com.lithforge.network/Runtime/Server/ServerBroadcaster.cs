using System;
using System.Collections.Generic;

using Lithforge.Network.Chunk;
using Lithforge.Network.Connection;
using Lithforge.Network.Messages;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Broadcasts player state, presence changes, and block change deltas to
    ///     connected peers each tick. Uses a spatial hash for O(K) proximity queries
    ///     instead of O(N²) full iteration. Internal helper owned by <see cref="ServerGameLoop" />.
    /// </summary>
    internal sealed class ServerBroadcaster
    {
        /// <summary>Pool of reusable int lists for the spatial hash cell entries.</summary>
        private readonly List<List<int>> _cellListPool = new();

        /// <summary>Reusable cache for collecting player IDs to despawn during presence broadcasts.</summary>
        private readonly List<ushort> _despawnCache = new();

        /// <summary>Delegate returning the current server tick number.</summary>
        private readonly Func<uint> _getCurrentTick;

        /// <summary>Reusable cache for host-side despawn notifications.</summary>
        private readonly List<ushort> _hostDespawnCache = new();

        /// <summary>Set of player IDs the host has spawned locally (mirrors SpawnedRemotePlayers on network peers).</summary>
        private readonly HashSet<ushort> _hostSpawnedPlayers = new();

        /// <summary>Reusable cache for indices of nearby players from the spatial hash.</summary>
        private readonly List<int> _nearbyCache = new();

        /// <summary>Reverse lookup from player ID to index in the playing peers list.</summary>
        private readonly Dictionary<ushort, int> _playerIdToIndex = new();

        /// <summary>The network server interface for sending messages.</summary>
        private readonly INetworkServer _server;

        /// <summary>Concrete NetworkServer for iterating all peers.</summary>
        private readonly NetworkServer _serverImpl;

        /// <summary>Bridge to gameplay simulation for reading player physics state.</summary>
        private readonly IServerSimulation _simulation;

        /// <summary>Spatial hash mapping coarse grid cells to player indices for O(K) proximity queries.</summary>
        private readonly Dictionary<int3, List<int>> _spatialCells = new();

        /// <summary>Cell size for the spatial hash (equals view radius).</summary>
        private readonly int _viewRadius;

        /// <summary>Current rental cursor into the cell list pool.</summary>
        private int _cellPoolCursor;

        /// <summary>Fires when a remote player leaves the host's view.</summary>
        internal Action<DespawnPlayerMessage> OnHostDespawnPlayer;

        /// <summary>Fires each tick with a remote player's authoritative state.</summary>
        internal Action<PlayerStateMessage> OnHostPlayerState;

        /// <summary>Fires when a remote player enters the host's view.</summary>
        internal Action<SpawnPlayerMessage> OnHostSpawnPlayer;

        /// <summary>Creates a new ServerBroadcaster with all required dependencies.</summary>
        internal ServerBroadcaster(
            INetworkServer server,
            NetworkServer serverImpl,
            IServerSimulation simulation,
            Func<uint> getCurrentTick,
            int viewRadius)
        {
            _server = server;
            _serverImpl = serverImpl;
            _simulation = simulation;
            _getCurrentTick = getCurrentTick;
            _viewRadius = viewRadius;
        }

        /// <summary>
        ///     Runs all broadcast phases for one tick: builds spatial index, then broadcasts
        ///     player states, presence changes, and block change deltas.
        /// </summary>
        internal void BroadcastAll(
            List<PeerInfo> playingPeers,
            Dictionary<int3, List<BlockChangeEntry>> dirtyChanges)
        {
            BuildSpatialIndex(playingPeers);
            BroadcastPlayerStates(playingPeers);
            BroadcastPlayerPresenceChanges(playingPeers);
            BroadcastBlockChanges(dirtyChanges);
        }

        /// <summary>
        ///     Removes a player from the host's spawned player set and fires the despawn callback.
        ///     Called by <see cref="ServerPeerLifecycle" /> when a player disconnects.
        /// </summary>
        internal void ClearHostSpawnedPlayer(ushort playerId)
        {
            if (_hostSpawnedPlayers.Remove(playerId))
            {
                OnHostDespawnPlayer?.Invoke(new DespawnPlayerMessage
                {
                    PlayerId = playerId,
                });
            }
        }

        /// <summary>Clears all host-local tracking state. Called by <see cref="ServerGameLoop.Dispose" />.</summary>
        internal void ClearAll()
        {
            _hostSpawnedPlayers.Clear();
            _hostDespawnCache.Clear();
        }

        /// <summary>
        ///     Phase 5a: sends each playing peer's authoritative state to the owning client
        ///     (for prediction reconciliation) and to spatially nearby observers.
        /// </summary>
        private void BroadcastPlayerStates(List<PeerInfo> playingPeers)
        {
            uint currentTick = _getCurrentTick();

            for (int i = 0; i < playingPeers.Count; i++)
            {
                PeerInfo peer = playingPeers[i];
                PlayerInterestState interest = peer.InterestState;

                if (interest is null)
                {
                    continue;
                }

                PlayerPhysicsState state = _simulation.GetPlayerState(peer.AssignedPlayerId);

                PlayerStateMessage msg = new()
                {
                    PlayerId = peer.AssignedPlayerId,
                    ServerTick = currentTick,
                    LastProcessedSeqId = interest.LastProcessedSequenceId,
                    PositionX = state.Position.x,
                    PositionY = state.Position.y,
                    PositionZ = state.Position.z,
                    VelocityX = state.Velocity.x,
                    VelocityY = state.Velocity.y,
                    VelocityZ = state.Velocity.z,
                    Yaw = state.Yaw,
                    Pitch = state.Pitch,
                    Flags = state.Flags,
                };

                // Send to the owning player (for prediction reconciliation).
                // Skip for local peer — no bridge latency to reconcile against.
                if (!peer.IsLocal)
                {
                    _server.SendTo(peer.ConnectionId, msg, PipelineId.UnreliableSequenced);
                }

                // Broadcast to spatially nearby playing peers (O(K) instead of O(N))
                GatherNearbyPlayerIndices(interest.CurrentChunk);

                for (int k = 0; k < _nearbyCache.Count; k++)
                {
                    int j = _nearbyCache[k];

                    if (j == i)
                    {
                        continue;
                    }

                    PeerInfo observer = playingPeers[j];
                    PlayerInterestState observerInterest = observer.InterestState;

                    if (observerInterest is null)
                    {
                        continue;
                    }

                    // Only send if the observer has the player's chunk loaded
                    if (observerInterest.LoadedChunks.Contains(interest.CurrentChunk))
                    {
                        _server.SendTo(observer.ConnectionId, msg, PipelineId.UnreliableSequenced);
                    }
                }

                // Notify host-local listener (host is not a network peer)
                OnHostPlayerState?.Invoke(msg);
            }
        }

        /// <summary>
        ///     Detects spawn/despawn edges for remote player entities by comparing each
        ///     observer's <see cref="PlayerInterestState.SpawnedRemotePlayers" /> against
        ///     the current interest region. Sends <see cref="SpawnPlayerMessage" /> when a
        ///     player enters and <see cref="DespawnPlayerMessage" /> when they leave.
        /// </summary>
        private void BroadcastPlayerPresenceChanges(List<PeerInfo> playingPeers)
        {
            for (int i = 0; i < playingPeers.Count; i++)
            {
                PeerInfo observer = playingPeers[i];
                PlayerInterestState observerInterest = observer.InterestState;

                if (observerInterest is null)
                {
                    continue;
                }

                // Spawn detection: check only spatially nearby players (O(K) instead of O(N))
                GatherNearbyPlayerIndices(observerInterest.CurrentChunk);

                for (int k = 0; k < _nearbyCache.Count; k++)
                {
                    int j = _nearbyCache[k];

                    if (j == i)
                    {
                        continue;
                    }

                    PeerInfo subject = playingPeers[j];
                    PlayerInterestState subjectInterest = subject.InterestState;

                    if (subjectInterest is null)
                    {
                        continue;
                    }

                    ushort subjectId = subject.AssignedPlayerId;
                    bool visible = observerInterest.LoadedChunks.Contains(subjectInterest.CurrentChunk);
                    bool alreadySpawned = observerInterest.SpawnedRemotePlayers.Contains(subjectId);

                    if (visible && !alreadySpawned)
                    {
                        PlayerPhysicsState state = _simulation.GetPlayerState(subjectId);

                        SpawnPlayerMessage msg = new()
                        {
                            PlayerId = subjectId,
                            PlayerName = subject.PlayerName,
                            PositionX = state.Position.x,
                            PositionY = state.Position.y,
                            PositionZ = state.Position.z,
                            Yaw = state.Yaw,
                            Pitch = state.Pitch,
                            Flags = state.Flags,
                        };

                        _server.SendTo(observer.ConnectionId, msg, PipelineId.ReliableSequenced);
                        observerInterest.SpawnedRemotePlayers.Add(subjectId);
                    }
                }

                // Despawn detection: iterate spawned remote players (typically small set, O(S))
                // Players who moved beyond the spatial neighborhood are NOT found by
                // GatherNearbyPlayerIndices, so we check SpawnedRemotePlayers directly.
                _despawnCache.Clear();

                foreach (ushort spawnedId in observerInterest.SpawnedRemotePlayers)
                {
                    if (!_playerIdToIndex.TryGetValue(spawnedId, out int subjectIdx))
                    {
                        // Player disconnected — handled by OnPlayerDisconnected
                        continue;
                    }

                    PlayerInterestState subjectInterest = playingPeers[subjectIdx].InterestState;

                    if (subjectInterest is null)
                    {
                        continue;
                    }

                    if (!observerInterest.LoadedChunks.Contains(subjectInterest.CurrentChunk))
                    {
                        _despawnCache.Add(spawnedId);
                    }
                }

                for (int k = 0; k < _despawnCache.Count; k++)
                {
                    ushort id = _despawnCache[k];

                    DespawnPlayerMessage msg = new()
                    {
                        PlayerId = id,
                    };

                    _server.SendTo(observer.ConnectionId, msg, PipelineId.ReliableSequenced);
                    observerInterest.SpawnedRemotePlayers.Remove(id);
                }
            }

            // The host has all chunks loaded locally, so all playing peers are visible.
            if (OnHostSpawnPlayer is not null || OnHostDespawnPlayer is not null)
            {
                // Spawn any playing peers the host hasn't seen yet
                for (int i = 0; i < playingPeers.Count; i++)
                {
                    PeerInfo peer = playingPeers[i];
                    ushort peerId = peer.AssignedPlayerId;

                    if (!_hostSpawnedPlayers.Add(peerId))
                    {
                        continue;
                    }

                    PlayerPhysicsState state = _simulation.GetPlayerState(peerId);

                    OnHostSpawnPlayer?.Invoke(new SpawnPlayerMessage
                    {
                        PlayerId = peerId,
                        PlayerName = peer.PlayerName,
                        PositionX = state.Position.x,
                        PositionY = state.Position.y,
                        PositionZ = state.Position.z,
                        Yaw = state.Yaw,
                        Pitch = state.Pitch,
                        Flags = state.Flags,
                    });
                }

                // Despawn players no longer in playing state
                _hostDespawnCache.Clear();

                foreach (ushort spawnedId in _hostSpawnedPlayers)
                {
                    bool stillPlaying = false;

                    for (int i = 0; i < playingPeers.Count; i++)
                    {
                        if (playingPeers[i].AssignedPlayerId == spawnedId)
                        {
                            stillPlaying = true;
                            break;
                        }
                    }

                    if (!stillPlaying)
                    {
                        _hostDespawnCache.Add(spawnedId);
                    }
                }

                for (int i = 0; i < _hostDespawnCache.Count; i++)
                {
                    ushort id = _hostDespawnCache[i];
                    OnHostDespawnPlayer?.Invoke(new DespawnPlayerMessage
                    {
                        PlayerId = id,
                    });
                    _hostSpawnedPlayers.Remove(id);
                }
            }
        }

        /// <summary>
        ///     Phase 5c: sends block change deltas to all playing peers whose interest region
        ///     includes the modified chunk. Single changes use BlockChangeMessage; batches use
        ///     MultiBlockChangeMessage. Skips the local peer (already predicted).
        /// </summary>
        private void BroadcastBlockChanges(Dictionary<int3, List<BlockChangeEntry>> dirtyChanges)
        {
            if (dirtyChanges.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<int3, List<BlockChangeEntry>> entry in dirtyChanges)
            {
                int3 chunkCoord = entry.Key;
                List<BlockChangeEntry> changes = entry.Value;

                if (changes.Count == 0)
                {
                    continue;
                }

                // Serialize block changes once
                if (changes.Count == 1)
                {
                    BlockChangeEntry change = changes[0];
                    BlockChangeMessage msg = new()
                    {
                        PositionX = change.Position.x, PositionY = change.Position.y, PositionZ = change.Position.z, NewState = change.NewState.Value,
                    };

                    // Send to all peers (Playing + Loading) who have this chunk loaded.
                    // Loading peers that already received the chunk need the delta too,
                    // otherwise they miss edits that happen while they finish loading.
                    // Skip the local peer — it already applied the change optimistically
                    // via ClientBlockPredictor and does not need the echo.
                    IReadOnlyList<PeerInfo> allPeers = _serverImpl.AllPeers;

                    for (int i = 0; i < allPeers.Count; i++)
                    {
                        PeerInfo peer = allPeers[i];

                        if (peer.StateMachine.Current != ConnectionState.Playing &&
                            peer.StateMachine.Current != ConnectionState.Loading)
                        {
                            continue;
                        }

                        if (peer.IsLocal)
                        {
                            continue;
                        }

                        if (peer.InterestState is not null &&
                            peer.InterestState.LoadedChunks.Contains(chunkCoord))
                        {
                            _server.SendTo(peer.ConnectionId, msg, PipelineId.ReliableSequenced);
                        }
                    }
                }
                else
                {
                    byte[] batchData = ChunkNetSerializer.SerializeBlockChangeBatch(chunkCoord, changes);
                    MultiBlockChangeMessage msg = new()
                    {
                        BatchData = batchData,
                    };

                    IReadOnlyList<PeerInfo> allPeers = _serverImpl.AllPeers;

                    for (int i = 0; i < allPeers.Count; i++)
                    {
                        PeerInfo peer = allPeers[i];

                        if (peer.StateMachine.Current != ConnectionState.Playing &&
                            peer.StateMachine.Current != ConnectionState.Loading)
                        {
                            continue;
                        }

                        if (peer.IsLocal)
                        {
                            continue;
                        }

                        if (peer.InterestState is not null &&
                            peer.InterestState.LoadedChunks.Contains(chunkCoord))
                        {
                            _server.SendTo(peer.ConnectionId, msg, PipelineId.ReliableSequenced);
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Populates the spatial hash from the given playing peers list.
        ///     Cell size equals <see cref="_viewRadius" /> so that a 3x3x3 cell
        ///     query covers all potential observers for any subject chunk.
        /// </summary>
        private void BuildSpatialIndex(List<PeerInfo> playingPeers)
        {
            // Return pooled lists and clear the dictionary
            _cellPoolCursor = 0;
            _spatialCells.Clear();
            _playerIdToIndex.Clear();

            for (int i = 0; i < playingPeers.Count; i++)
            {
                PeerInfo peer = playingPeers[i];
                PlayerInterestState interest = peer.InterestState;

                if (interest is null)
                {
                    continue;
                }

                _playerIdToIndex[peer.AssignedPlayerId] = i;

                int3 cell = ChunkToCell(interest.CurrentChunk, _viewRadius);

                if (!_spatialCells.TryGetValue(cell, out List<int> list))
                {
                    list = RentCellList();
                    _spatialCells[cell] = list;
                }

                list.Add(i);
            }
        }

        /// <summary>Rents a cleared list from the cell list pool, growing the pool if needed.</summary>
        private List<int> RentCellList()
        {
            if (_cellPoolCursor < _cellListPool.Count)
            {
                List<int> list = _cellListPool[_cellPoolCursor];
                list.Clear();
                _cellPoolCursor++;
                return list;
            }

            List<int> newList = new();
            _cellListPool.Add(newList);
            _cellPoolCursor++;
            return newList;
        }

        /// <summary>
        ///     Fills <see cref="_nearbyCache" /> with indices into the playing peers list
        ///     for all players whose <see cref="PlayerInterestState.CurrentChunk" /> falls within
        ///     a 3x3x3 cell neighborhood of the given chunk coordinate.
        /// </summary>
        private void GatherNearbyPlayerIndices(int3 subjectChunk)
        {
            _nearbyCache.Clear();
            int3 centerCell = ChunkToCell(subjectChunk, _viewRadius);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int3 cell = centerCell + new int3(dx, dy, dz);

                        if (_spatialCells.TryGetValue(cell, out List<int> list))
                        {
                            for (int k = 0; k < list.Count; k++)
                            {
                                _nearbyCache.Add(list[k]);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Converts a chunk coordinate to a spatial hash cell coordinate by integer floor division.</summary>
        private static int3 ChunkToCell(int3 chunkCoord, int cellSize)
        {
            return new int3(
                FloorDiv(chunkCoord.x, cellSize),
                FloorDiv(chunkCoord.y, cellSize),
                FloorDiv(chunkCoord.z, cellSize));
        }

        /// <summary>Integer floor division that rounds toward negative infinity for negative dividends.</summary>
        private static int FloorDiv(int a, int b)
        {
            if (a >= 0)
            {
                return a / b;
            }

            return (a - b + 1) / b;
        }

        /// <summary>Counts the number of peers currently in Playing state.</summary>
        internal int CountPlayingPeers()
        {
            int count = 0;
            IReadOnlyList<PeerInfo> allPeers = _serverImpl.AllPeers;

            for (int i = 0; i < allPeers.Count; i++)
            {
                if (allPeers[i].StateMachine.Current == ConnectionState.Playing)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
