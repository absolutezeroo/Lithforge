using System;
using System.Collections.Generic;

using Lithforge.Network.Bridge;
using Lithforge.Network.Connection;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Command;
using Lithforge.Voxel.Storage;

using Unity.Mathematics;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Manages peer lifecycle events (accept, ready, disconnect), player state
    ///     persistence, chunk streaming orchestration, and chunk snapshot publishing.
    ///     Internal helper owned by <see cref="ServerGameLoop" />.
    /// </summary>
    internal sealed class ServerPeerLifecycle
    {
        /// <summary>Interval (in ticks) between periodic save sweeps.</summary>
        private const int SaveIntervalTicks = 900;

        /// <summary>Block command processor for canceling digs and removing player state.</summary>
        private readonly IServerBlockProcessor _blockProcessor;

        /// <summary>Cross-thread bridge for publishing player chunk positions (nullable).</summary>
        private readonly ServerThreadBridge _bridge;

        /// <summary>Provider for querying chunk readiness and spawning.</summary>
        private readonly IServerChunkProvider _chunkProvider;

        /// <summary>Delegate returning the current server tick number.</summary>
        private readonly Func<uint> _getCurrentTick;

        /// <summary>Delegate returning the streaming strategy for a given connection.</summary>
        private readonly Func<ConnectionId, IChunkStreamingStrategy> _getStrategy;

        /// <summary>Reusable cache for peers in Loading state.</summary>
        private readonly List<PeerInfo> _loadingPeersCache = new();

        /// <summary>Logger instance for diagnostic messages.</summary>
        private readonly ILogger _logger;

        /// <summary>Delegate to clear a disconnected player from the broadcaster's host tracking.</summary>
        private readonly Action<ushort> _onClearHostSpawnedPlayer;

        /// <summary>Delegate fired when the number of playing peers changes.</summary>
        private readonly Action<int> _onPlayerCountChanged;

        /// <summary>Tracks per-peer Loading-state timeouts for force-transitioning unresponsive clients.</summary>
        private readonly ClientReadinessWaiter _readinessWaiter;

        /// <summary>The network server interface for sending messages.</summary>
        private readonly INetworkServer _server;

        /// <summary>Concrete NetworkServer for peer lookup and iteration.</summary>
        private readonly NetworkServer _serverImpl;

        /// <summary>Bridge to gameplay simulation for physics and time-of-day queries.</summary>
        private readonly IServerSimulation _simulation;

        /// <summary>Manages per-player chunk streaming queues and rate limiting.</summary>
        private readonly ChunkStreamingManager _streamingManager;

        /// <summary>Server-side inventory processor (null until set).</summary>
        private ServerInventoryProcessor _inventoryProcessor;

        /// <summary>Cached current time from the most recent tick, for use in TransitionToPlaying.</summary>
        private float _lastCurrentTime;

        /// <summary>Tick counter for periodic save scheduling. Resets after each save sweep.</summary>
        private int _ticksSinceLastSave;

        /// <summary>
        ///     Fires after a player is accepted and initialized (physics body created,
        ///     streaming queue built). Parameters: PeerInfo, spawn position.
        /// </summary>
        internal Action<PeerInfo, float3> OnPlayerAcceptedCallback;

        /// <summary>
        ///     Delegate for persisting a player's state to disk. Called on the server thread
        ///     for both disconnect saves and periodic saves. Parameters: (playerUuid, state).
        /// </summary>
        internal Action<string, WorldPlayerState> OnSavePlayerState;

        /// <summary>Creates a new ServerPeerLifecycle with all required dependencies.</summary>
        internal ServerPeerLifecycle(
            INetworkServer server,
            NetworkServer serverImpl,
            IServerSimulation simulation,
            IServerBlockProcessor blockProcessor,
            IServerChunkProvider chunkProvider,
            ChunkStreamingManager streamingManager,
            ClientReadinessWaiter readinessWaiter,
            ServerThreadBridge bridge,
            ILogger logger,
            Func<uint> getCurrentTick,
            Func<ConnectionId, IChunkStreamingStrategy> getStrategy,
            Action<int> onPlayerCountChanged,
            Action<ushort> onClearHostSpawnedPlayer)
        {
            _server = server;
            _serverImpl = serverImpl;
            _simulation = simulation;
            _blockProcessor = blockProcessor;
            _chunkProvider = chunkProvider;
            _streamingManager = streamingManager;
            _readinessWaiter = readinessWaiter;
            _bridge = bridge;
            _logger = logger;
            _getCurrentTick = getCurrentTick;
            _getStrategy = getStrategy;
            _onPlayerCountChanged = onPlayerCountChanged;
            _onClearHostSpawnedPlayer = onClearHostSpawnedPlayer;
        }

        /// <summary>Injects the server-side inventory processor for player accept and capture.</summary>
        internal void SetInventoryProcessor(ServerInventoryProcessor processor)
        {
            _inventoryProcessor = processor;
        }

        /// <summary>
        ///     Caches the current wall-clock time for use by message handlers that fire
        ///     during <see cref="INetworkServer.Update" /> (e.g. OnClientReady).
        ///     Must be called at the start of each tick before the server update pump.
        /// </summary>
        internal void SetCurrentTime(float currentTime)
        {
            _lastCurrentTime = currentTime;
        }

        /// <summary>Registers the 2 lifecycle-related message handlers on the dispatcher.</summary>
        internal void RegisterMessageHandlers(MessageDispatcher dispatcher)
        {
            dispatcher.RegisterHandler(MessageType.ClientReady, OnClientReady);
            dispatcher.RegisterHandler(MessageType.ChunkBatchAck, OnChunkBatchAck);
        }

        /// <summary>
        ///     Callback wired to NetworkServer.OnPeerAccepted. Computes the spawn
        ///     position and delegates to OnPlayerAccepted.
        /// </summary>
        internal void OnPeerAccepted(PeerInfo peer)
        {
            float3 spawnPosition;

            if (peer.PlayerData is not null)
            {
                // Restore to the player's last saved position
                spawnPosition = new float3(
                    peer.PlayerData.PosX,
                    peer.PlayerData.PosY,
                    peer.PlayerData.PosZ);
            }
            else
            {
                // No saved data — use world spawn
                int spawnX = 0;
                int spawnZ = 0;
                int safeY = _chunkProvider.FindSafeSpawnY(
                    spawnX, spawnZ,
                    _streamingManager.YLoadMin, _streamingManager.YLoadMax,
                    65);

                spawnPosition = new float3(spawnX + 0.5f, safeY, spawnZ + 0.5f);
            }

            OnPlayerAccepted(peer, spawnPosition);
        }

        /// <summary>
        ///     Called when a new player's handshake is accepted. Creates the physics body
        ///     and initializes chunk streaming.
        /// </summary>
        internal void OnPlayerAccepted(PeerInfo peer, float3 spawnPosition)
        {
            ushort playerId = peer.AssignedPlayerId;
            uint currentTick = _getCurrentTick();

            // Create interest state
            peer.InterestState = new PlayerInterestState(ServerGameLoop.DefaultViewRadius)
            {
                CurrentChunk = new int3(
                    (int)math.floor(spawnPosition.x / ChunkConstants.Size),
                    (int)math.floor(spawnPosition.y / ChunkConstants.Size),
                    (int)math.floor(spawnPosition.z / ChunkConstants.Size)),
            };

            // Create physics body on the server
            _simulation.AddPlayer(playerId, spawnPosition);

            // Initialize chunk streaming centered on spawn
            _streamingManager.InitializeForPlayer(peer.InterestState, spawnPosition);

            _logger.LogInfo(
                $"Player {playerId} ({peer.PlayerName}) accepted, spawning at {spawnPosition}");

            // Send spawn position early so the client can configure its readiness tracker
            // before any ChunkData messages arrive. Both local and remote peers receive this
            // (DirectTransport for SP/Host delivers it in the same frame).
            SpawnInitMessage spawnInit = new()
            {
                SpawnX = spawnPosition.x, SpawnY = spawnPosition.y, SpawnZ = spawnPosition.z, ClientReadyRadius = (byte)math.min(_streamingManager.ReadyRadius, byte.MaxValue),
            };

            _server.SendTo(peer.ConnectionId, spawnInit, PipelineId.ReliableSequenced);

            // Create server-side inventory, restore saved data, and send initial full sync
            if (_inventoryProcessor is not null)
            {
                _inventoryProcessor.GetOrCreateInventory(playerId);

                if (peer.PlayerData is not null)
                {
                    _inventoryProcessor.RestoreInventoryFromSave(playerId, peer.PlayerData);
                }

                _inventoryProcessor.InitializePlayerState(peer);
            }

            // Register with the readiness waiter for timeout enforcement
            _readinessWaiter.OnPeerEnteredLoading(peer.ConnectionId, currentTick);

            OnPlayerAcceptedCallback?.Invoke(peer, spawnPosition);
        }

        /// <summary>
        ///     Callback wired to NetworkServer.OnPeerRemoved. Captures player state
        ///     for persistence before cleaning up the physics body and broadcasting despawn.
        /// </summary>
        internal void OnPeerRemoved(PeerInfo peer)
        {
            ushort playerId = peer.AssignedPlayerId;

            if (playerId == 0)
            {
                return;
            }

            // Only capture state if the peer reached Loading/Playing (has a physics body).
            // Peers disconnecting during Handshaking/Authenticating have no body — capturing
            // would overwrite valid saved data with a zero-position default.
            ConnectionState currentState = peer.StateMachine.Current;
            bool hasPhysicsBody = currentState is ConnectionState.Loading or ConnectionState.Playing;

            if (hasPhysicsBody)
            {
                WorldPlayerState captured = CapturePlayerState(peer);

                if (captured is not null && !string.IsNullOrEmpty(peer.PlayerUuid))
                {
                    OnSavePlayerState?.Invoke(peer.PlayerUuid, captured);
                }
            }

            PlayerDisconnected(playerId);
        }

        /// <summary>
        ///     Called when a player disconnects. Removes the physics body, cancels any
        ///     in-progress digging, notifies all observers to despawn the remote entity,
        ///     and clears the host's spawned player tracking.
        /// </summary>
        internal void PlayerDisconnected(ushort playerId)
        {
            _blockProcessor.CancelDigging(playerId);
            _simulation.RemovePlayer(playerId);
            _inventoryProcessor?.ReturnCursorToInventory(playerId);
            _inventoryProcessor?.RemoveInventory(playerId);

            // Notify all observers to despawn this player
            IReadOnlyList<PeerInfo> allPeers = _serverImpl.AllPeers;

            for (int i = 0; i < allPeers.Count; i++)
            {
                PeerInfo observer = allPeers[i];
                PlayerInterestState observerInterest = observer.InterestState;

                if (observerInterest is null)
                {
                    continue;
                }

                if (observerInterest.SpawnedRemotePlayers.Remove(playerId))
                {
                    DespawnPlayerMessage msg = new()
                    {
                        PlayerId = playerId,
                    };

                    _server.SendTo(observer.ConnectionId, msg, PipelineId.ReliableSequenced);
                }
            }

            // Notify host-local listener via broadcaster delegate
            _onClearHostSpawnedPlayer(playerId);

            _onPlayerCountChanged(CountPlayingPeers());
        }

        /// <summary>
        ///     Captures the current state of a connected player for persistence.
        ///     Reads position/rotation from the simulation and inventory from the processor.
        ///     Returns null if the player has no UUID or no physics state.
        /// </summary>
        internal WorldPlayerState CapturePlayerState(PeerInfo peer)
        {
            if (string.IsNullOrEmpty(peer.PlayerUuid))
            {
                return null;
            }

            PlayerPhysicsState physState = _simulation.GetPlayerState(peer.AssignedPlayerId);
            float timeOfDay = _simulation.GetTimeOfDay();

            WorldPlayerState state = new()
            {
                PosX = physState.Position.x,
                PosY = physState.Position.y,
                PosZ = physState.Position.z,
                RotX = physState.Pitch,
                RotY = physState.Yaw,
                TimeOfDay = timeOfDay,
            };

            // Serialize inventory if available
            if (_inventoryProcessor is not null)
            {
                _inventoryProcessor.SerializeInventoryInto(peer.AssignedPlayerId, state);
            }

            return state;
        }

        /// <summary>
        ///     Saves all currently playing players via the <see cref="OnSavePlayerState" /> delegate.
        ///     Called periodically from the server thread and once on shutdown.
        /// </summary>
        internal void SaveAllPlayers()
        {
            if (OnSavePlayerState is null)
            {
                return;
            }

            IReadOnlyList<PeerInfo> peers = _serverImpl.AllPeers;

            for (int i = 0; i < peers.Count; i++)
            {
                PeerInfo peer = peers[i];

                if (peer.StateMachine.Current != ConnectionState.Playing)
                {
                    continue;
                }

                WorldPlayerState state = CapturePlayerState(peer);

                if (state is not null && !string.IsNullOrEmpty(peer.PlayerUuid))
                {
                    OnSavePlayerState(peer.PlayerUuid, state);
                }
            }
        }

        /// <summary>
        ///     Increments the periodic save counter and triggers a full save sweep
        ///     when the threshold is reached.
        /// </summary>
        internal void TickPeriodicSave()
        {
            _ticksSinceLastSave++;

            if (_ticksSinceLastSave >= SaveIntervalTicks)
            {
                _ticksSinceLastSave = 0;
                SaveAllPlayers();
            }
        }

        /// <summary>
        ///     Builds and publishes the per-player chunk coordinate snapshot to the bridge
        ///     after player inputs have been processed and CurrentChunk values are up-to-date.
        ///     Allocates a fresh array per tick to avoid torn reads on the main thread.
        /// </summary>
        internal void PublishSnapshot(List<PeerInfo> playingPeers)
        {
            if (_bridge is null)
            {
                return;
            }

            int count = playingPeers.Count;

            // Also include Loading peers (they have a valid CurrentChunk from spawn init)
            GatherPeersByState(_loadingPeersCache, ConnectionState.Loading);
            int totalPeers = count + _loadingPeersCache.Count;

            if (totalPeers == 0)
            {
                _bridge.SetPlayerChunkSnapshot(PlayerChunkSnapshot.Empty);
                return;
            }

            // Allocate fresh array to avoid torn reads (small: ~12 bytes per player)
            int3[] coords = new int3[totalPeers];
            int filled = 0;

            for (int i = 0; i < playingPeers.Count; i++)
            {
                PlayerInterestState interest = playingPeers[i].InterestState;

                if (interest is not null)
                {
                    coords[filled] = interest.CurrentChunk;
                    filled++;
                }
            }

            for (int i = 0; i < _loadingPeersCache.Count; i++)
            {
                PlayerInterestState interest = _loadingPeersCache[i].InterestState;

                if (interest is not null)
                {
                    coords[filled] = interest.CurrentChunk;
                    filled++;
                }
            }

            _bridge.SetPlayerChunkSnapshot(new PlayerChunkSnapshot(coords, filled));
        }

        /// <summary>
        ///     Drives per-peer chunk streaming for all Loading and Playing peers,
        ///     and enforces the ClientReady timeout for Loading peers.
        /// </summary>
        internal void ProcessStreamingAndReadiness(float currentTime)
        {
            _lastCurrentTime = currentTime;
            IReadOnlyList<PeerInfo> allPeers = _serverImpl.AllPeers;

            for (int i = 0; i < allPeers.Count; i++)
            {
                PeerInfo peer = allPeers[i];
                ConnectionState state = peer.StateMachine.Current;

                if (state is ConnectionState.Loading or ConnectionState.Playing)
                {
                    IChunkStreamingStrategy strategy = _getStrategy(peer.ConnectionId);
                    uint currentTick = _getCurrentTick();
                    _streamingManager.ProcessForPeer(peer, strategy, currentTick);

                    // Client-side readiness gating: the client sends ClientReady when it has
                    // received enough chunks. Server only acts on timeout (30s fallback).
                    if (state == ConnectionState.Loading)
                    {
                        if (_readinessWaiter.IsTimedOut(peer.ConnectionId, currentTick))
                        {
                            _logger.LogWarning(
                                $"ClientReady timeout for peer {peer.ConnectionId}, forcing Playing");
                            _readinessWaiter.OnPeerReady(peer.ConnectionId);

                            TransitionToPlaying(peer, currentTime);
                        }
                    }
                }
            }
        }

        /// <summary>Handles ClientReady messages from Loading peers, triggering transition to Playing.</summary>
        private void OnClientReady(ConnectionId connId, byte[] data, int offset, int length)
        {
            PeerInfo peer = _serverImpl.GetPeer(connId);

            if (peer is null || peer.StateMachine.Current != ConnectionState.Loading)
            {
                return;
            }

            ClientReadyMessage msg = ClientReadyMessage.Deserialize(data, offset, length);
            _logger.LogInfo(
                $"ClientReady from peer {connId} (radius={msg.ReadyRadius})");
            _readinessWaiter.OnPeerReady(connId);
            TransitionToPlaying(peer, _lastCurrentTime);
        }

        /// <summary>Handles ChunkBatchAck messages, decrementing the peer's in-flight chunk count.</summary>
        private void OnChunkBatchAck(ConnectionId connId, byte[] data, int offset, int length)
        {
            PeerInfo peer = _serverImpl.GetPeer(connId);

            if (peer?.InterestState is null)
            {
                return;
            }

            ChunkBatchAckMessage msg = ChunkBatchAckMessage.Deserialize(data, offset, length);
            PlayerInterestState interest = peer.InterestState;
            interest.UnackedChunks -= msg.Count;

            if (interest.UnackedChunks < 0)
            {
                interest.UnackedChunks = 0;
            }
        }

        /// <summary>
        ///     Transitions a Loading peer to Playing state, clears initial load mode,
        ///     sends the GameReady message, and fires the player count changed event.
        /// </summary>
        private void TransitionToPlaying(PeerInfo peer, float currentTime)
        {
            bool transitioned = peer.StateMachine.Transition(ConnectionState.Playing, currentTime);

            if (!transitioned)
            {
                _logger.LogWarning(
                    $"Failed to transition peer {peer.ConnectionId} from Loading to Playing");
                return;
            }

            PlayerInterestState interest = peer.InterestState;
            interest.IsInitialLoad = false;
            uint currentTick = _getCurrentTick();

            GameReadyMessage msg = new()
            {
                SpawnX = interest.SpawnPosition.x,
                SpawnY = interest.SpawnPosition.y,
                SpawnZ = interest.SpawnPosition.z,
                TimeOfDay = _simulation.GetTimeOfDay(),
                ServerTick = currentTick,
            };

            _server.SendTo(peer.ConnectionId, msg, PipelineId.ReliableSequenced);

            _logger.LogInfo(
                $"Player {peer.AssignedPlayerId} ({peer.PlayerName}) transitioned to Playing at tick {currentTick}");

            _onPlayerCountChanged(CountPlayingPeers());
        }

        /// <summary>Counts the number of peers currently in Playing state.</summary>
        private int CountPlayingPeers()
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

        /// <summary>Fills the result list with all peers matching the target connection state.</summary>
        private void GatherPeersByState(List<PeerInfo> result, ConnectionState targetState)
        {
            result.Clear();
            IReadOnlyList<PeerInfo> allPeers = _serverImpl.AllPeers;

            for (int i = 0; i < allPeers.Count; i++)
            {
                if (allPeers[i].StateMachine.Current == targetState)
                {
                    result.Add(allPeers[i]);
                }
            }
        }
    }
}
