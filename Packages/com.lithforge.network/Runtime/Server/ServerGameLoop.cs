using System;
using System.Collections.Generic;

using Lithforge.Network.Bridge;
using Lithforge.Network.Chunk;
using Lithforge.Network.Connection;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Server-authoritative 6-phase tick loop running at 30 TPS.
    ///     Owns no Tier 3 types — accesses gameplay simulation via <see cref="IServerSimulation" />
    ///     and chunk data via <see cref="IServerChunkProvider" />.
    ///     Phase 1: Drain network input (NetworkServer.Update)
    ///     Phase 2: Process player inputs (movement + block commands)
    ///     Phase 3: Simulate world (non-player tick systems)
    ///     Phase 4: Gather dirty state (ChunkDirtyTracker.FlushAll)
    ///     Phase 5: Broadcast updates (player states, block changes, chunk streaming)
    ///     Phase 6: Flush network output (implicit — UTP batches within frame)
    /// </summary>
    public sealed class ServerGameLoop : IDisposable
    {
        /// <summary>
        ///     Fixed delta time per server tick (1/30 second).
        /// </summary>
        private const float TickDt = 1f / 30f;

        /// <summary>
        ///     Maximum accumulated time to prevent spiral-of-death catch-up (5 ticks).
        /// </summary>
        private const float MaxAccumulatedTime = TickDt * 5f;

        /// <summary>
        ///     Maximum block interaction reach distance in blocks.
        /// </summary>
        private const float MaxReachDistance = 6f;

        /// <summary>
        ///     Default chunk view radius assigned to new players.
        /// </summary>
        private const int DefaultViewRadius = 10;

        /// <summary>
        ///     Server-side block command processor for validating and executing block changes.
        /// </summary>
        private readonly IServerBlockProcessor _blockProcessor;

        /// <summary>
        ///     Pool of reusable int lists for the spatial hash cell entries.
        /// </summary>
        private readonly List<List<int>> _cellListPool = new();

        /// <summary>
        ///     Provider for querying chunk readiness and spawning.
        /// </summary>
        private readonly IServerChunkProvider _chunkProvider;

        /// <summary>
        ///     Reusable cache for collecting player IDs to despawn during presence broadcasts.
        /// </summary>
        private readonly List<ushort> _despawnCache = new();

        /// <summary>
        ///     Reusable cache for dirty chunk coordinates.
        /// </summary>
        private readonly List<int3> _dirtiedChunksCache = new();

        /// <summary>
        ///     Source of per-tick dirty block changes for network delta batching.
        /// </summary>
        private readonly IDirtyChangeSource _dirtyTracker;

        /// <summary>
        ///     Reusable cache for host-side despawn notifications.
        /// </summary>
        private readonly List<ushort> _hostDespawnCache = new();

        /// <summary>
        ///     Set of player IDs the host has spawned locally (mirrors SpawnedRemotePlayers on network peers).
        /// </summary>
        private readonly HashSet<ushort> _hostSpawnedPlayers = new();

        /// <summary>
        ///     Reusable cache for peers in Loading state.
        /// </summary>
        private readonly List<PeerInfo> _loadingPeersCache = new();

        /// <summary>
        ///     Logger instance for diagnostic messages.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        ///     Reusable cache for indices of nearby players from the spatial hash.
        /// </summary>
        private readonly List<int> _nearbyCache = new();

        /// <summary>Per-connection streaming strategy override. Falls back to _defaultStrategy.</summary>
        private readonly Dictionary<int, IChunkStreamingStrategy> _peerStrategies = new();
        /// <summary>
        ///     Reverse lookup from player ID to index in _playingPeersCache.
        /// </summary>
        private readonly Dictionary<ushort, int> _playerIdToIndex = new();

        /// <summary>
        ///     Reusable cache for peers in Playing state, filled each tick.
        /// </summary>
        private readonly List<PeerInfo> _playingPeersCache = new();

        /// <summary>
        ///     Tracks per-peer Loading-state timeouts for force-transitioning unresponsive clients.
        /// </summary>
        private readonly ClientReadinessWaiter _readinessWaiter;

        /// <summary>
        ///     The network server interface for sending messages and managing peers.
        /// </summary>
        private readonly INetworkServer _server;

        /// <summary>
        ///     Concrete NetworkServer reference for internal operations (peer lookup, etc.).
        /// </summary>
        private readonly NetworkServer _serverImpl;

        /// <summary>
        ///     Bridge to the gameplay simulation for player physics and world tick systems.
        /// </summary>
        private readonly IServerSimulation _simulation;

        /// <summary>
        ///     Spatial hash mapping coarse grid cells to player indices for O(K) proximity queries.
        /// </summary>
        private readonly Dictionary<int3, List<int>> _spatialCells = new();

        /// <summary>
        ///     Manages per-player chunk streaming queues and rate limiting.
        /// </summary>
        private readonly ChunkStreamingManager _streamingManager;

        /// <summary>
        ///     Current rental cursor into the cell list pool.
        /// </summary>
        private int _cellPoolCursor;

        /// <summary>
        ///     Default chunk streaming strategy used for peers without a per-connection override.
        /// </summary>
        private IChunkStreamingStrategy _defaultStrategy;

        /// <summary>
        ///     Whether this game loop has been disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        ///     The currentTime value from the most recent Update call, cached for use in callbacks.
        /// </summary>
        private float _lastCurrentTime;

        /// <summary>
        ///     Debug counter for stream operations (diagnostic only).
        /// </summary>
        private int _streamDebugCounter;

        /// <summary>
        ///     Accumulated time for fixed-rate tick scheduling.
        /// </summary>
        private float _tickAccumulator;

        /// <summary>Fires when a remote player leaves the host's view.</summary>
        public Action<DespawnPlayerMessage> OnHostDespawnPlayer;

        /// <summary>Fires each tick with a remote player's authoritative state.</summary>
        public Action<PlayerStateMessage> OnHostPlayerState;

        /// <summary>Fires when a remote player enters the host's view.</summary>
        public Action<SpawnPlayerMessage> OnHostSpawnPlayer;

        /// <summary>
        ///     Fires after a player is accepted and initialized (physics body created,
        ///     streaming queue built). Parameters: PeerInfo, spawn position.
        ///     Used by NetworkServerSubsystem to teleport the local player to spawn.
        /// </summary>
        public Action<PeerInfo, float3> OnPlayerAcceptedCallback;

        // Host-local callbacks: let the host see remote players without a NetworkClient.
        // Uses existing message structs (Tier 2) to avoid primitive-soup signatures.

        /// <summary>Fired when the number of playing peers changes. Parameter is the new count.</summary>
        public Action<int> OnPlayerCountChanged;

        /// <summary>
        ///     Creates a new ServerGameLoop wiring together all server-side subsystems.
        /// </summary>
        public ServerGameLoop(
            NetworkServer server,
            IServerSimulation simulation,
            IServerBlockProcessor blockProcessor,
            IServerChunkProvider chunkProvider,
            IDirtyChangeSource dirtyTracker,
            ChunkStreamingManager streamingManager,
            IChunkStreamingStrategy defaultStrategy,
            ClientReadinessWaiter readinessWaiter,
            ILogger logger)
        {
            _server = server;
            _serverImpl = server;
            _simulation = simulation;
            _blockProcessor = blockProcessor;
            _chunkProvider = chunkProvider;
            _dirtyTracker = dirtyTracker;
            _streamingManager = streamingManager;
            _defaultStrategy = defaultStrategy;
            _readinessWaiter = readinessWaiter;
            _logger = logger;

            // Register gameplay message handlers
            MessageDispatcher dispatcher = server.Dispatcher;
            dispatcher.RegisterHandler(MessageType.MoveInput, OnMoveInput);
            dispatcher.RegisterHandler(MessageType.PlaceBlockCmd, OnPlaceBlockCmd);
            dispatcher.RegisterHandler(MessageType.BreakBlockCmd, OnBreakBlockCmd);
            dispatcher.RegisterHandler(MessageType.StartDiggingCmd, OnStartDiggingCmd);
            dispatcher.RegisterHandler(MessageType.ClientReady, OnClientReady);
            dispatcher.RegisterHandler(MessageType.ChunkBatchAck, OnChunkBatchAck);

            _serverImpl.OnPeerAccepted = OnPeerAcceptedInternal;
        }

        /// <summary>
        ///     The current server tick number, starting at 1.
        /// </summary>
        public uint CurrentTick { get; private set; } = 1;

        /// <summary>
        ///     Disposes this game loop, clearing all callbacks and host-local tracking.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _hostSpawnedPlayers.Clear();
                _hostDespawnCache.Clear();
                _serverImpl.OnPeerAccepted = null;
                OnPlayerCountChanged = null;
                OnPlayerAcceptedCallback = null;
                OnHostSpawnPlayer = null;
                OnHostDespawnPlayer = null;
                OnHostPlayerState = null;
            }
        }

        /// <summary>
        ///     Sets the default streaming strategy used for peers without a per-connection override.
        /// </summary>
        public void SetDefaultStrategy(IChunkStreamingStrategy strategy)
        {
            _defaultStrategy = strategy;
        }

        /// <summary>
        ///     Registers a per-connection streaming strategy override (e.g. LocalChunkStreamingStrategy
        ///     for the local peer in always-server mode).
        /// </summary>
        public void SetPeerStrategy(ConnectionId connectionId, IChunkStreamingStrategy strategy)
        {
            _peerStrategies[connectionId.Value] = strategy;
        }

        /// <summary>
        ///     Called once per frame. Accumulates time and runs 0-5 ticks per call.
        ///     For a dedicated server, call from the main loop with wall-clock delta.
        ///     For a listen server, call from GameLoop.Update().
        /// </summary>
        public void Update(float deltaTime, float currentTime)
        {
            _tickAccumulator += deltaTime;

            // Spiral-of-death cap: prevent runaway catch-up
            if (_tickAccumulator > MaxAccumulatedTime)
            {
                _tickAccumulator = MaxAccumulatedTime;
            }

            while (_tickAccumulator >= TickDt)
            {
                ExecuteOneTick(currentTime);
                _tickAccumulator -= TickDt;
            }
        }

        /// <summary>Runs one full 6-phase server tick at the fixed tick rate.</summary>
        internal void ExecuteOneTick(float currentTime)
        {
            _lastCurrentTime = currentTime;

            // Phase 1: Drain network input
            _server.Update(currentTime);
            _server.CurrentTick = CurrentTick;

            // Phase 2: Process player inputs
            GatherPeersByState(_playingPeersCache, ConnectionState.Playing);
            ProcessPlayerInputs();

            // Post-input hook: allows BridgedSimulation to synchronize physics results
            (_simulation as IPostInputHook)?.AfterProcessPlayerInputs();

            // Build spatial index for Phase 5 broadcast (O(N) instead of O(N²) iteration)
            BuildSpatialIndex();

            // Phase 3: Simulate world
            _simulation.TickWorldSystems(TickDt);

            // Phase 4: Gather dirty state
            Dictionary<int3, List<BlockChangeEntry>> dirtyChanges = _dirtyTracker.FlushAll();

            // Phase 5: Broadcast updates
            BroadcastPlayerStates();
            BroadcastPlayerPresenceChanges();
            BroadcastBlockChanges(dirtyChanges);
            ProcessChunkStreaming(currentTime);

            // Phase 6: implicit — UTP batches sends within frame
            CurrentTick++;
        }

        /// <summary>
        ///     Phase 2: drains each playing peer's move buffer, applies authoritative movement
        ///     via the simulation, updates chunk coordinates, and processes block commands.
        /// </summary>
        private void ProcessPlayerInputs()
        {
            for (int i = 0; i < _playingPeersCache.Count; i++)
            {
                PeerInfo peer = _playingPeersCache[i];
                PlayerInterestState interest = peer.InterestState;

                if (interest == null)
                {
                    continue;
                }

                ushort playerId = peer.AssignedPlayerId;

                // Process movement: try to get buffered input for this tick
                float yaw;
                float pitch;
                byte flags;
                ushort seqId;

                if (interest.MoveBuffer.TryGet(CurrentTick, out MoveCommand moveCmd))
                {
                    yaw = moveCmd.LookDir.x;
                    pitch = moveCmd.LookDir.y;
                    flags = moveCmd.Flags;
                    seqId = moveCmd.SequenceId;
                    interest.LastKnownInputFlags = flags;
                    interest.LastKnownLookDir = moveCmd.LookDir;
                    interest.LastProcessedSequenceId = seqId;
                }
                else
                {
                    // Input repeat: use last known input (player continues moving)
                    yaw = interest.LastKnownLookDir.x;
                    pitch = interest.LastKnownLookDir.y;
                    flags = interest.LastKnownInputFlags;
                    seqId = interest.LastProcessedSequenceId;
                }

                PlayerPhysicsState authState = _simulation.ApplyMoveInput(
                    playerId, yaw, pitch, flags, TickDt);

                // Update current chunk from authoritative position
                interest.CurrentChunk = new int3(
                    (int)math.floor(authState.Position.x / ChunkConstants.Size),
                    (int)math.floor(authState.Position.y / ChunkConstants.Size),
                    (int)math.floor(authState.Position.z / ChunkConstants.Size));

                // Process block commands (movement first so reach check uses updated position)
                ProcessBlockCommands(peer, interest, authState);
            }
        }

        /// <summary>
        ///     Drains pending StartDigging, BreakBlock, and PlaceBlock commands for a single
        ///     peer, validating each through the block processor and sending ACKs.
        /// </summary>
        private void ProcessBlockCommands(PeerInfo peer, PlayerInterestState interest, PlayerPhysicsState playerState)
        {
            ushort playerId = peer.AssignedPlayerId;
            _blockProcessor.RefillRateLimitTokens(playerId, CurrentTick * TickDt);

            // Process StartDigging commands first (must precede break commands in same tick)
            for (int i = 0; i < interest.PendingStartDiggingCommands.Count; i++)
            {
                StartDiggingCommand cmd = interest.PendingStartDiggingCommands[i];
                _blockProcessor.StartDigging(playerId, cmd.Position, playerState.Position, CurrentTick);
            }

            interest.PendingStartDiggingCommands.Clear();

            // Process break commands
            for (int i = 0; i < interest.PendingBreakCommands.Count; i++)
            {
                BreakBlockCommand cmd = interest.PendingBreakCommands[i];

                BlockProcessResult result = _blockProcessor.TryBreakBlock(
                    playerId, cmd.Position, playerState.Position, CurrentTick);

                AcknowledgeBlockChangeMessage ack = new()
                {
                    SequenceId = cmd.SequenceId,
                    Accepted = (byte)(result.Accepted ? 1 : 0),
                    PositionX = cmd.Position.x,
                    PositionY = cmd.Position.y,
                    PositionZ = cmd.Position.z,
                    CorrectedState = result.Accepted
                        ? result.AcceptedState.Value
                        : _blockProcessor.GetBlock(cmd.Position).Value,
                };

                _server.SendTo(peer.ConnectionId, ack, PipelineId.ReliableSequenced);
            }

            interest.PendingBreakCommands.Clear();

            // Process place commands
            for (int i = 0; i < interest.PendingPlaceCommands.Count; i++)
            {
                PlaceBlockCommand cmd = interest.PendingPlaceCommands[i];

                BlockProcessResult result = _blockProcessor.TryPlaceBlock(
                    playerId, cmd.Position, cmd.BlockState, cmd.Face, playerState.Position);

                AcknowledgeBlockChangeMessage ack = new()
                {
                    SequenceId = cmd.SequenceId,
                    Accepted = (byte)(result.Accepted ? 1 : 0),
                    PositionX = cmd.Position.x,
                    PositionY = cmd.Position.y,
                    PositionZ = cmd.Position.z,
                    CorrectedState = result.Accepted
                        ? result.AcceptedState.Value
                        : _blockProcessor.GetBlock(cmd.Position).Value,
                };

                _server.SendTo(peer.ConnectionId, ack, PipelineId.ReliableSequenced);
            }

            interest.PendingPlaceCommands.Clear();
        }

        /// <summary>
        ///     Phase 5a: sends each playing peer's authoritative state to the owning client
        ///     (for prediction reconciliation) and to spatially nearby observers.
        /// </summary>
        private void BroadcastPlayerStates()
        {
            for (int i = 0; i < _playingPeersCache.Count; i++)
            {
                PeerInfo peer = _playingPeersCache[i];
                PlayerInterestState interest = peer.InterestState;

                if (interest == null)
                {
                    continue;
                }

                PlayerPhysicsState state = _simulation.GetPlayerState(peer.AssignedPlayerId);

                PlayerStateMessage msg = new()
                {
                    PlayerId = peer.AssignedPlayerId,
                    ServerTick = CurrentTick,
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

                // Send to the owning player (for prediction reconciliation)
                _server.SendTo(peer.ConnectionId, msg, PipelineId.UnreliableSequenced);

                // Broadcast to spatially nearby playing peers (O(K) instead of O(N))
                GatherNearbyPlayerIndices(interest.CurrentChunk);

                for (int k = 0; k < _nearbyCache.Count; k++)
                {
                    int j = _nearbyCache[k];

                    if (j == i)
                    {
                        continue;
                    }

                    PeerInfo observer = _playingPeersCache[j];
                    PlayerInterestState observerInterest = observer.InterestState;

                    if (observerInterest == null)
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
        private void BroadcastPlayerPresenceChanges()
        {
            for (int i = 0; i < _playingPeersCache.Count; i++)
            {
                PeerInfo observer = _playingPeersCache[i];
                PlayerInterestState observerInterest = observer.InterestState;

                if (observerInterest == null)
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

                    PeerInfo subject = _playingPeersCache[j];
                    PlayerInterestState subjectInterest = subject.InterestState;

                    if (subjectInterest == null)
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

                    PlayerInterestState subjectInterest = _playingPeersCache[subjectIdx].InterestState;

                    if (subjectInterest == null)
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
            if (OnHostSpawnPlayer != null || OnHostDespawnPlayer != null)
            {
                // Spawn any playing peers the host hasn't seen yet
                for (int i = 0; i < _playingPeersCache.Count; i++)
                {
                    PeerInfo peer = _playingPeersCache[i];
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

                    for (int i = 0; i < _playingPeersCache.Count; i++)
                    {
                        if (_playingPeersCache[i].AssignedPlayerId == spawnedId)
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

                        if (peer.InterestState != null &&
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

                        if (peer.InterestState != null &&
                            peer.InterestState.LoadedChunks.Contains(chunkCoord))
                        {
                            _server.SendTo(peer.ConnectionId, msg, PipelineId.ReliableSequenced);
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Phase 5d: drives per-peer chunk streaming for all Loading and Playing peers,
        ///     and enforces the ClientReady timeout for Loading peers.
        /// </summary>
        private void ProcessChunkStreaming(float currentTime)
        {
            IReadOnlyList<PeerInfo> allPeers = _serverImpl.AllPeers;

            for (int i = 0; i < allPeers.Count; i++)
            {
                PeerInfo peer = allPeers[i];
                ConnectionState state = peer.StateMachine.Current;

                if (state == ConnectionState.Loading || state == ConnectionState.Playing)
                {
                    IChunkStreamingStrategy strategy = GetStrategyForPeer(peer.ConnectionId);
                    _streamingManager.ProcessForPeer(peer, strategy, CurrentTick);

                    // Client-side readiness gating: the client sends ClientReady when it has
                    // received enough chunks. Server only acts on timeout (30s fallback).
                    if (state == ConnectionState.Loading)
                    {
                        if (_readinessWaiter.IsTimedOut(peer.ConnectionId, CurrentTick))
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

            GameReadyMessage msg = new()
            {
                SpawnX = interest.SpawnPosition.x,
                SpawnY = interest.SpawnPosition.y,
                SpawnZ = interest.SpawnPosition.z,
                TimeOfDay = _simulation.GetTimeOfDay(),
                ServerTick = CurrentTick,
            };

            _server.SendTo(peer.ConnectionId, msg, PipelineId.ReliableSequenced);

            _logger.LogInfo(
                $"Player {peer.AssignedPlayerId} ({peer.PlayerName}) transitioned to Playing at tick {CurrentTick}");

            OnPlayerCountChanged?.Invoke(CountPlayingPeers());
        }

        /// <summary>Handles ClientReady messages from Loading peers, triggering transition to Playing.</summary>
        private void OnClientReady(ConnectionId connId, byte[] data, int offset, int length)
        {
            PeerInfo peer = _serverImpl.GetPeer(connId);

            if (peer == null || peer.StateMachine.Current != ConnectionState.Loading)
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

            if (peer?.InterestState == null)
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

        /// <summary>Handles MoveInput messages, buffering the command for the owning peer's tick processing.</summary>
        private void OnMoveInput(ConnectionId connId, byte[] data, int offset, int length)
        {
            _serverImpl.TouchPeer(connId);
            PeerInfo peer = _serverImpl.GetPeer(connId);

            if (peer?.InterestState == null ||
                peer.StateMachine.Current != ConnectionState.Playing)
            {
                return;
            }

            MoveInputMessage msg = MoveInputMessage.Deserialize(data, offset, length);
            MoveCommand cmd = new()
            {
                Tick = CurrentTick,
                SequenceId = msg.SequenceId,
                PlayerId = peer.AssignedPlayerId,
                Position = float3.zero, // Server computes position authoritatively
                LookDir = new float2(msg.Yaw, msg.Pitch),
                Flags = msg.Flags,
            };

            peer.InterestState.MoveBuffer.Add(CurrentTick, cmd);
        }

        /// <summary>Handles PlaceBlockCmd messages, queuing the command for Phase 2 block processing.</summary>
        private void OnPlaceBlockCmd(ConnectionId connId, byte[] data, int offset, int length)
        {
            _serverImpl.TouchPeer(connId);
            PeerInfo peer = _serverImpl.GetPeer(connId);

            if (peer?.InterestState == null ||
                peer.StateMachine.Current != ConnectionState.Playing)
            {
                return;
            }

            PlaceBlockCmdMessage msg = PlaceBlockCmdMessage.Deserialize(data, offset, length);

            PlaceBlockCommand cmd = new()
            {
                Tick = CurrentTick,
                SequenceId = msg.SequenceId,
                PlayerId = peer.AssignedPlayerId,
                Position = new int3(msg.PositionX, msg.PositionY, msg.PositionZ),
                BlockState = new StateId(msg.BlockState),
                Face = (BlockFace)msg.Face,
            };

            peer.InterestState.PendingPlaceCommands.Add(cmd);
        }

        /// <summary>Handles BreakBlockCmd messages, queuing the command for Phase 2 block processing.</summary>
        private void OnBreakBlockCmd(ConnectionId connId, byte[] data, int offset, int length)
        {
            _serverImpl.TouchPeer(connId);
            PeerInfo peer = _serverImpl.GetPeer(connId);

            if (peer?.InterestState == null ||
                peer.StateMachine.Current != ConnectionState.Playing)
            {
                return;
            }

            BreakBlockCmdMessage msg = BreakBlockCmdMessage.Deserialize(data, offset, length);

            BreakBlockCommand cmd = new()
            {
                Tick = CurrentTick, SequenceId = msg.SequenceId, PlayerId = peer.AssignedPlayerId, Position = new int3(msg.PositionX, msg.PositionY, msg.PositionZ),
            };

            peer.InterestState.PendingBreakCommands.Add(cmd);
        }

        /// <summary>Handles StartDiggingCmd messages, queuing the command for Phase 2 block processing.</summary>
        private void OnStartDiggingCmd(ConnectionId connId, byte[] data, int offset, int length)
        {
            _serverImpl.TouchPeer(connId);
            PeerInfo peer = _serverImpl.GetPeer(connId);

            if (peer?.InterestState == null ||
                peer.StateMachine.Current != ConnectionState.Playing)
            {
                return;
            }

            StartDiggingCmdMessage msg = StartDiggingCmdMessage.Deserialize(data, offset, length);

            StartDiggingCommand cmd = new()
            {
                Tick = CurrentTick, SequenceId = msg.SequenceId, PlayerId = peer.AssignedPlayerId, Position = new int3(msg.PositionX, msg.PositionY, msg.PositionZ),
            };

            peer.InterestState.PendingStartDiggingCommands.Add(cmd);
        }

        /// <summary>
        ///     Internal callback wired to NetworkServer.OnPeerAccepted. Computes the spawn
        ///     position and delegates to OnPlayerAccepted.
        /// </summary>
        private void OnPeerAcceptedInternal(PeerInfo peer)
        {
            int spawnX = 0;
            int spawnZ = 0;
            int safeY = _chunkProvider.FindSafeSpawnY(
                spawnX, spawnZ,
                _streamingManager.YLoadMin, _streamingManager.YLoadMax,
                65);

            float3 spawnPosition = new(spawnX + 0.5f, safeY, spawnZ + 0.5f);
            OnPlayerAccepted(peer, spawnPosition);
        }

        /// <summary>
        ///     Called when a new player's handshake is accepted. Creates the physics body
        ///     and initializes chunk streaming.
        /// </summary>
        public void OnPlayerAccepted(PeerInfo peer, float3 spawnPosition)
        {
            ushort playerId = peer.AssignedPlayerId;

            // Create interest state
            peer.InterestState = new PlayerInterestState(DefaultViewRadius);

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

            // Register with the readiness waiter for timeout enforcement
            _readinessWaiter.OnPeerEnteredLoading(peer.ConnectionId, CurrentTick);

            OnPlayerAcceptedCallback?.Invoke(peer, spawnPosition);
        }

        /// <summary>
        ///     Called when a player disconnects. Removes the physics body, cancels any
        ///     in-progress digging, and notifies all observers to despawn the remote entity.
        /// </summary>
        public void OnPlayerDisconnected(ushort playerId)
        {
            _blockProcessor.CancelDigging(playerId);
            _simulation.RemovePlayer(playerId);

            // Notify all observers to despawn this player
            IReadOnlyList<PeerInfo> allPeers = _serverImpl.AllPeers;

            for (int i = 0; i < allPeers.Count; i++)
            {
                PeerInfo observer = allPeers[i];
                PlayerInterestState observerInterest = observer.InterestState;

                if (observerInterest == null)
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

            // Notify host-local listener
            if (_hostSpawnedPlayers.Remove(playerId))
            {
                OnHostDespawnPlayer?.Invoke(new DespawnPlayerMessage
                {
                    PlayerId = playerId,
                });
            }

            OnPlayerCountChanged?.Invoke(CountPlayingPeers());
        }

        /// <summary>
        ///     Populates the spatial hash from <see cref="_playingPeersCache" />.
        ///     Cell size equals <see cref="DefaultViewRadius" /> so that a 3x3x3 cell
        ///     query covers all potential observers for any subject chunk.
        /// </summary>
        private void BuildSpatialIndex()
        {
            // Return pooled lists and clear the dictionary
            _cellPoolCursor = 0;
            _spatialCells.Clear();
            _playerIdToIndex.Clear();

            for (int i = 0; i < _playingPeersCache.Count; i++)
            {
                PeerInfo peer = _playingPeersCache[i];
                PlayerInterestState interest = peer.InterestState;

                if (interest == null)
                {
                    continue;
                }

                _playerIdToIndex[peer.AssignedPlayerId] = i;

                int3 cell = ChunkToCell(interest.CurrentChunk, DefaultViewRadius);

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
        ///     Fills <see cref="_nearbyCache" /> with indices into <see cref="_playingPeersCache" />
        ///     for all players whose <see cref="PlayerInterestState.CurrentChunk" /> falls within
        ///     a 3x3x3 cell neighborhood of the given chunk coordinate.
        /// </summary>
        private void GatherNearbyPlayerIndices(int3 subjectChunk)
        {
            _nearbyCache.Clear();
            int3 centerCell = ChunkToCell(subjectChunk, DefaultViewRadius);

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

        /// <summary>Returns the per-connection strategy override if registered, otherwise the default strategy.</summary>
        private IChunkStreamingStrategy GetStrategyForPeer(ConnectionId connectionId)
        {
            if (_peerStrategies.TryGetValue(connectionId.Value, out IChunkStreamingStrategy strategy))
            {
                return strategy;
            }

            return _defaultStrategy;
        }
    }
}
