using System;
using System.Collections.Generic;

using Lithforge.Core.Logging;
using Lithforge.Network.Chunk;
using Lithforge.Network.Connection;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

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
        private const float TickDt = 1f / 30f;
        private const float MaxAccumulatedTime = TickDt * 5f;
        private const float MaxReachDistance = 6f;
        private const int DefaultViewRadius = 10;
        private readonly IServerBlockProcessor _blockProcessor;
        private readonly IServerChunkProvider _chunkProvider;
        private readonly List<int3> _dirtiedChunksCache = new();
        private readonly ChunkDirtyTracker _dirtyTracker;
        private readonly List<ushort> _hostDespawnCache = new();

        // Host-local entity tracking (mirrors SpawnedRemotePlayers on network peers)
        private readonly HashSet<ushort> _hostSpawnedPlayers = new();
        private readonly List<PeerInfo> _loadingPeersCache = new();
        private readonly ILogger _logger;

        // Cached collections (fill pattern, reused every tick)
        private readonly List<PeerInfo> _playingPeersCache = new();

        private readonly INetworkServer _server;
        private readonly NetworkServer _serverImpl;
        private readonly IServerSimulation _simulation;
        private readonly ChunkStreamingManager _streamingManager;

        /// <summary>Per-connection streaming strategy override. Falls back to _defaultStrategy.</summary>
        private readonly Dictionary<int, IChunkStreamingStrategy> _peerStrategies = new();
        private IChunkStreamingStrategy _defaultStrategy;

        private bool _disposed;
        private int _streamDebugCounter;
        private float _tickAccumulator;

        /// <summary>Fires when a remote player leaves the host's view.</summary>
        public Action<DespawnPlayerMessage> OnHostDespawnPlayer;

        /// <summary>Fires each tick with a remote player's authoritative state.</summary>
        public Action<PlayerStateMessage> OnHostPlayerState;

        // Host-local callbacks: let the host see remote players without a NetworkClient.
        // Uses existing message structs (Tier 2) to avoid primitive-soup signatures.

        /// <summary>Fired when the number of playing peers changes. Parameter is the new count.</summary>
        public Action<int> OnPlayerCountChanged;

        /// <summary>Fires when a remote player enters the host's view.</summary>
        public Action<SpawnPlayerMessage> OnHostSpawnPlayer;

        public ServerGameLoop(
            NetworkServer server,
            IServerSimulation simulation,
            IServerBlockProcessor blockProcessor,
            IServerChunkProvider chunkProvider,
            ChunkDirtyTracker dirtyTracker,
            ChunkStreamingManager streamingManager,
            IChunkStreamingStrategy defaultStrategy,
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
            _logger = logger;

            // Register gameplay message handlers
            MessageDispatcher dispatcher = server.Dispatcher;
            dispatcher.RegisterHandler(MessageType.MoveInput, OnMoveInput);
            dispatcher.RegisterHandler(MessageType.PlaceBlockCmd, OnPlaceBlockCmd);
            dispatcher.RegisterHandler(MessageType.BreakBlockCmd, OnBreakBlockCmd);
            dispatcher.RegisterHandler(MessageType.StartDiggingCmd, OnStartDiggingCmd);

            _serverImpl.OnPeerAccepted = OnPeerAcceptedInternal;
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

        public uint CurrentTick { get; private set; } = 1;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _hostSpawnedPlayers.Clear();
                _hostDespawnCache.Clear();
                _serverImpl.OnPeerAccepted = null;
                OnPlayerCountChanged = null;
                OnHostSpawnPlayer = null;
                OnHostDespawnPlayer = null;
                OnHostPlayerState = null;
            }
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
                ExecuteTick(currentTime);
                _tickAccumulator -= TickDt;
            }
        }

        private void ExecuteTick(float currentTime)
        {
            // Phase 1: Drain network input
            _server.Update(currentTime);
            _server.CurrentTick = CurrentTick;

            // Phase 2: Process player inputs
            GatherPeersByState(_playingPeersCache, ConnectionState.Playing);
            ProcessPlayerInputs();

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

        // ── Phase 2: Process player inputs ──

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

        // ── Phase 5: Broadcast updates ──

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

                // Broadcast to other playing peers
                for (int j = 0; j < _playingPeersCache.Count; j++)
                {
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

                for (int j = 0; j < _playingPeersCache.Count; j++)
                {
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
                    else if (!visible && alreadySpawned)
                    {
                        DespawnPlayerMessage msg = new()
                        {
                            PlayerId = subjectId,
                        };

                        _server.SendTo(observer.ConnectionId, msg, PipelineId.ReliableSequenced);
                        observerInterest.SpawnedRemotePlayers.Remove(subjectId);
                    }
                }
            }

            // ── Host-local presence tracking ──
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

                    // Send to all players who have this chunk loaded
                    for (int i = 0; i < _playingPeersCache.Count; i++)
                    {
                        PeerInfo peer = _playingPeersCache[i];

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

                    for (int i = 0; i < _playingPeersCache.Count; i++)
                    {
                        PeerInfo peer = _playingPeersCache[i];

                        if (peer.InterestState != null &&
                            peer.InterestState.LoadedChunks.Contains(chunkCoord))
                        {
                            _server.SendTo(peer.ConnectionId, msg, PipelineId.ReliableSequenced);
                        }
                    }
                }
            }
        }

        private void ProcessChunkStreaming(float currentTime)
        {
            IReadOnlyList<PeerInfo> allPeers = _serverImpl.AllPeers;
            bool shouldLog = _streamDebugCounter++ % 30 == 0;

            if (shouldLog)
            {
                int peerCount = allPeers.Count;

                if (peerCount > 0)
                {
                    bool anyActive = false;

                    for (int i = 0; i < peerCount; i++)
                    {
                        ConnectionState s = allPeers[i].StateMachine.Current;

                        if (s == ConnectionState.Loading || s == ConnectionState.Playing)
                        {
                            anyActive = true;
                            break;
                        }
                    }

                    if (!anyActive)
                    {
                        string states = "";

                        for (int i = 0; i < peerCount; i++)
                        {
                            if (i > 0)
                            {
                                states += ", ";
                            }

                            states += $"{allPeers[i].ConnectionId}={allPeers[i].StateMachine.Current}";
                        }

                        UnityEngine.Debug.Log(
                            $"[STREAM] {peerCount} peer(s) but none in Loading/Playing. States: {states}");
                    }
                }
            }

            for (int i = 0; i < allPeers.Count; i++)
            {
                PeerInfo peer = allPeers[i];
                ConnectionState state = peer.StateMachine.Current;

                if (state == ConnectionState.Loading || state == ConnectionState.Playing)
                {
                    if (shouldLog)
                    {
                        if (peer.InterestState != null)
                        {
                            UnityEngine.Debug.Log(
                                $"[STREAM] peer={peer.ConnectionId} state={state} " +
                                $"queue={peer.InterestState.StreamingQueue.Count} " +
                                $"loaded={peer.InterestState.LoadedChunks.Count} " +
                                $"idx={peer.InterestState.StreamingQueueIndex}");
                        }
                        else
                        {
                            UnityEngine.Debug.Log(
                                $"[STREAM] peer={peer.ConnectionId} state={state} InterestState=NULL");
                        }
                    }

                    IChunkStreamingStrategy strategy = GetStrategyForPeer(peer.ConnectionId);
                    _streamingManager.ProcessForPeer(peer, strategy, CurrentTick);

                    // Check if Loading peer is ready to transition to Playing
                    if (state == ConnectionState.Loading &&
                        _streamingManager.IsReadyForPlaying(peer, _chunkProvider))
                    {
                        TransitionToPlaying(peer, currentTime);
                    }
                }
            }
        }

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

        // ── Message handlers ──

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

        // ── Player lifecycle ──

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

        // ── Helpers ──

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
