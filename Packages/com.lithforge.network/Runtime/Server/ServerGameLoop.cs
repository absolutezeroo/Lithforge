using System;
using System.Collections.Generic;

using Lithforge.Item.Crafting;
using Lithforge.Network.Bridge;
using Lithforge.Network.Chunk;
using Lithforge.Network.Connection;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using Lithforge.Voxel.Storage;

using Unity.Mathematics;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Server-authoritative 6-phase tick loop running at 30 TPS.
    ///     Thin facade delegating to <see cref="ServerInputProcessor" />,
    ///     <see cref="ServerBroadcaster" />, and <see cref="ServerPeerLifecycle" />.
    ///     Phase 1: Drain network input (NetworkServer.Update)
    ///     Phase 2: Process player inputs (movement + block commands)
    ///     Phase 3: Simulate world (non-player tick systems)
    ///     Phase 4: Gather dirty state (ChunkDirtyTracker.FlushAll)
    ///     Phase 5: Broadcast updates (player states, block changes, chunk streaming)
    ///     Phase 6: Flush network output (implicit — UTP batches within frame)
    /// </summary>
    public sealed class ServerGameLoop : IDisposable
    {
        /// <summary>Fixed delta time per server tick (1/30 second).</summary>
        private const float TickDt = 1f / 30f;

        /// <summary>Maximum accumulated time to prevent spiral-of-death catch-up (5 ticks).</summary>
        private const float MaxAccumulatedTime = TickDt * 5f;

        /// <summary>Default chunk view radius assigned to new players.</summary>
        internal const int DefaultViewRadius = 10;

        /// <summary>Player state, presence, and block change broadcaster.</summary>
        private readonly ServerBroadcaster _broadcaster;

        /// <summary>Source of per-tick dirty block changes for network delta batching.</summary>
        private readonly IDirtyChangeSource _dirtyTracker;

        /// <summary>Input processing and block command handler.</summary>
        private readonly ServerInputProcessor _inputProcessor;

        /// <summary>Server-side inventory processor for per-tick delta sync (null until set).</summary>
        private ServerInventoryProcessor _inventoryProcessor;

        /// <summary>Peer accept, ready, disconnect, and persistence handler.</summary>
        private readonly ServerPeerLifecycle _lifecycle;

        /// <summary>Per-connection streaming strategy override. Falls back to _defaultStrategy.</summary>
        private readonly Dictionary<int, IChunkStreamingStrategy> _peerStrategies = new();

        /// <summary>Reusable cache for peers in Playing state, filled each tick.</summary>
        private readonly List<PeerInfo> _playingPeersCache = new();

        /// <summary>The network server interface for sending messages and managing peers.</summary>
        private readonly INetworkServer _server;

        /// <summary>Concrete NetworkServer reference for internal operations (peer lookup, etc.).</summary>
        private readonly NetworkServer _serverImpl;

        /// <summary>Bridge to the gameplay simulation for player physics and world tick systems.</summary>
        private readonly IServerSimulation _simulation;

        /// <summary>Manages per-player chunk streaming queues and rate limiting.</summary>
        private readonly ChunkStreamingManager _streamingManager;

        /// <summary>Default chunk streaming strategy used for peers without a per-connection override.</summary>
        private IChunkStreamingStrategy _defaultStrategy;

        /// <summary>Whether this game loop has been disposed.</summary>
        private bool _disposed;

        /// <summary>Accumulated time for fixed-rate tick scheduling.</summary>
        private float _tickAccumulator;

        /// <summary>Fired when the number of playing peers changes. Parameter is the new count.</summary>
        public Action<int> OnPlayerCountChanged;

        /// <summary>Creates a new ServerGameLoop wiring together all server-side subsystems.</summary>
        internal ServerGameLoop(
            NetworkServer server,
            IServerSimulation simulation,
            IServerBlockProcessor blockProcessor,
            IServerChunkProvider chunkProvider,
            IDirtyChangeSource dirtyTracker,
            ChunkStreamingManager streamingManager,
            IChunkStreamingStrategy defaultStrategy,
            ClientReadinessWaiter readinessWaiter,
            ServerThreadBridge bridge,
            ILogger logger)
        {
            _server = server;
            _serverImpl = server;
            _simulation = simulation;
            _dirtyTracker = dirtyTracker;
            _streamingManager = streamingManager;
            _defaultStrategy = defaultStrategy;

            _inputProcessor = new ServerInputProcessor(
                server, server, simulation, blockProcessor, () => CurrentTick, bridge);

            _broadcaster = new ServerBroadcaster(
                server, server, simulation, () => CurrentTick, DefaultViewRadius);

            _lifecycle = new ServerPeerLifecycle(
                server, server, simulation, blockProcessor, chunkProvider,
                streamingManager, readinessWaiter, bridge, logger,
                () => CurrentTick,
                connId => GetStrategyForPeer(connId),
                count => OnPlayerCountChanged?.Invoke(count),
                playerId => _broadcaster.ClearHostSpawnedPlayer(playerId));

            // Register all message handlers
            MessageDispatcher dispatcher = server.Dispatcher;
            _inputProcessor.RegisterMessageHandlers(dispatcher);
            _lifecycle.RegisterMessageHandlers(dispatcher);

            _serverImpl.OnPeerAccepted = _lifecycle.OnPeerAccepted;
            _serverImpl.OnPeerRemoved += _lifecycle.OnPeerRemoved;
        }

        /// <summary>Fires when a remote player enters the host's view.</summary>
        public Action<SpawnPlayerMessage> OnHostSpawnPlayer
        {
            get { return _broadcaster.OnHostSpawnPlayer; }
            set { _broadcaster.OnHostSpawnPlayer = value; }
        }

        /// <summary>Fires when a remote player leaves the host's view.</summary>
        public Action<DespawnPlayerMessage> OnHostDespawnPlayer
        {
            get { return _broadcaster.OnHostDespawnPlayer; }
            set { _broadcaster.OnHostDespawnPlayer = value; }
        }

        /// <summary>Fires each tick with a remote player's authoritative state.</summary>
        public Action<PlayerStateMessage> OnHostPlayerState
        {
            get { return _broadcaster.OnHostPlayerState; }
            set { _broadcaster.OnHostPlayerState = value; }
        }

        /// <summary>
        ///     Fires after a player is accepted and initialized (physics body created,
        ///     streaming queue built). Parameters: PeerInfo, spawn position.
        ///     Used by NetworkServerSubsystem to teleport the local player to spawn.
        /// </summary>
        public Action<PeerInfo, float3> OnPlayerAcceptedCallback
        {
            get { return _lifecycle.OnPlayerAcceptedCallback; }
            set { _lifecycle.OnPlayerAcceptedCallback = value; }
        }

        /// <summary>
        ///     Delegate for persisting a player's state to disk. Called on the server thread
        ///     for both disconnect saves and periodic saves. Parameters: (playerUuid, state).
        /// </summary>
        public Action<string, WorldPlayerState> OnSavePlayerState
        {
            get { return _lifecycle.OnSavePlayerState; }
            set { _lifecycle.OnSavePlayerState = value; }
        }

        /// <summary>The current server tick number, starting at 1.</summary>
        public uint CurrentTick { get; private set; } = 1;

        /// <summary>Disposes this game loop, clearing all callbacks and host-local tracking.</summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _broadcaster.ClearAll();
                _serverImpl.OnPeerAccepted = null;
                OnPlayerCountChanged = null;
                OnPlayerAcceptedCallback = null;
                OnHostSpawnPlayer = null;
                OnHostDespawnPlayer = null;
                OnHostPlayerState = null;
            }
        }

        /// <summary>Injects the server-side inventory processor for slot click command handling.</summary>
        public void SetInventoryProcessor(ServerInventoryProcessor processor)
        {
            _inventoryProcessor = processor;
            _inputProcessor.SetInventoryProcessor(processor);
            _lifecycle.SetInventoryProcessor(processor);
        }

        /// <summary>Injects the crafting engine for server-side recipe validation.</summary>
        public void SetCraftingEngine(CraftingEngine craftingEngine)
        {
            _inventoryProcessor?.SetCraftingEngine(craftingEngine);
        }

        /// <summary>Injects container dependencies (container manager, block entity provider, simulation).</summary>
        public void SetContainerDependencies(
            ServerContainerManager containerManager,
            IServerBlockEntityProvider blockEntityProvider)
        {
            _inventoryProcessor?.SetContainerDependencies(
                containerManager, blockEntityProvider, _simulation);
        }

        /// <summary>Sets the default streaming strategy used for peers without a per-connection override.</summary>
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
            // Cache time before Phase 1 so message handlers see the current value
            _lifecycle.SetCurrentTime(currentTime);

            // Phase 1: Drain network input
            _server.Update(currentTime);
            _server.CurrentTick = CurrentTick;

            // Phase 2: Process player inputs
            GatherPeersByState(_playingPeersCache, ConnectionState.Playing);
            _inputProcessor.ProcessTick(_playingPeersCache, TickDt);

            // Publish player chunk snapshot for main thread consumption (after CurrentChunk is updated)
            _lifecycle.PublishSnapshot(_playingPeersCache);

            // Post-input hook: allows BridgedSimulation to synchronize physics results
            (_simulation as IPostInputHook)?.AfterProcessPlayerInputs();

            // Phase 3: Simulate world
            _simulation.TickWorldSystems(TickDt);

            // Phase 4: Gather dirty state
            Dictionary<int3, List<BlockChangeEntry>> dirtyChanges = _dirtyTracker.FlushAll();

            // Phase 5: Broadcast updates
            _broadcaster.BroadcastAll(_playingPeersCache, dirtyChanges);
            _inventoryProcessor?.BroadcastInventoryDeltas(_playingPeersCache);
            _inventoryProcessor?.BroadcastContainerDeltas();
            _lifecycle.ProcessStreamingAndReadiness(currentTime);

            // Phase 6: implicit — UTP batches sends within frame
            CurrentTick++;

            // Periodic save
            _lifecycle.TickPeriodicSave();
        }

        /// <summary>
        ///     Called when a new player's handshake is accepted. Creates the physics body
        ///     and initializes chunk streaming.
        /// </summary>
        public void OnPlayerAccepted(PeerInfo peer, float3 spawnPosition)
        {
            _lifecycle.OnPlayerAccepted(peer, spawnPosition);
        }

        /// <summary>
        ///     Called when a player disconnects. Removes the physics body, cancels any
        ///     in-progress digging, and notifies all observers to despawn the remote entity.
        /// </summary>
        public void OnPlayerDisconnected(ushort playerId)
        {
            _lifecycle.PlayerDisconnected(playerId);
        }

        /// <summary>
        ///     Captures the current state of a connected player for persistence.
        ///     Reads position/rotation from the simulation and inventory from the processor.
        ///     Returns null if the player has no UUID or no physics state.
        /// </summary>
        public WorldPlayerState CapturePlayerState(PeerInfo peer)
        {
            return _lifecycle.CapturePlayerState(peer);
        }

        /// <summary>
        ///     Saves all currently playing players via the <see cref="OnSavePlayerState" /> delegate.
        ///     Called periodically from the server thread and once on shutdown by ServerThreadRunner.
        /// </summary>
        internal void SaveAllPlayers()
        {
            _lifecycle.SaveAllPlayers();
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
