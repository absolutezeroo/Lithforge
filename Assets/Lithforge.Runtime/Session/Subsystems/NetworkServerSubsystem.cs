using System;
using System.Collections.Generic;

using Lithforge.Network;
using Lithforge.Network.Bridge;
using Lithforge.Network.Chunk;
using Lithforge.Network.Server;
using Lithforge.Network.Transport;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Network;
using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;

using Unity.Mathematics;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>
    ///     Subsystem that creates the network server, transport layer, and server game loop
    ///     for singleplayer (DirectTransport), host (Direct+UTP), and dedicated server modes.
    /// </summary>
    public sealed class NetworkServerSubsystem : IGameSubsystem
    {
        /// <summary>Composite transport combining direct and UTP transports.</summary>
        private CompositeTransport _compositeTransport;

        /// <summary>Direct in-memory server transport for local player connection.</summary>
        private DirectTransportServer _directServer;

        /// <summary>
        ///     Set in PostInitialize; used by the accept callback to teleport the
        ///     player to spawn so that generation centers on the correct position.
        /// </summary>
        private PlayerTransformHolder _playerHolder;

        /// <summary>Stored reference to the real transport for the bridge pump (DedicatedServer case).</summary>
        private INetworkTransport _realTransport;

        /// <summary>The owned network server managing connections and message dispatch.</summary>
        private NetworkServer _server;

        /// <summary>Background server thread runner (null until PostInitialize starts it).</summary>
        private ServerThreadRunner _runner;

        /// <summary>The server-side game loop processing player commands and chunk streaming.</summary>
        private ServerGameLoop _serverGameLoop;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "NetworkServer";
            }
        }

        /// <summary>Depends on chunk manager and tick registry for server simulation.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkManagerSubsystem),
            typeof(TickRegistrySubsystem),
        };

        /// <summary>Created for singleplayer, host, and dedicated server modes.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            // Always-server: singleplayer now runs a server too
            return config is SessionConfig.Singleplayer
                or SessionConfig.Host
                or SessionConfig.DedicatedServer;
        }

        /// <summary>Creates the server, transport layer, and game loop based on session mode.</summary>
        public void Initialize(SessionContext context)
        {
            ILogger logger = context.App.Logger;
            ContentHash contentHash = ContentHashComputer.Compute(context.Content.StateRegistry);

            int maxPlayers = context.Config switch
            {
                SessionConfig.Host host => host.MaxPlayers,
                SessionConfig.DedicatedServer ds => ds.MaxPlayers,
                _ => 8,
            };

            _server = new NetworkServer(logger, contentHash, maxPlayers);

            if (context.TryGet(out WorldMetadata metadata))
            {
                _server.WorldSeed = (ulong)metadata.Seed;
            }

            // Set up transport based on session mode
            if (context.Config is SessionConfig.Singleplayer)
            {
                // Direct in-memory transport only
                DirectTransportPair.Create(out _directServer, out DirectTransportClient directClient);
                _compositeTransport = new CompositeTransport();
                _compositeTransport.AddTransport(_directServer);
                _directServer.Listen(0);
                _server.StartWithTransport(_compositeTransport);

                // Store the client transport for NetworkClientSubsystem to retrieve
                context.Register(directClient);
            }
            else if (context.Config is SessionConfig.Host host)
            {
                // DirectTransport for local player + UTP for remote players
                DirectTransportPair.Create(out _directServer, out DirectTransportClient directClient);
                NetworkDriverWrapper utpTransport = new(logger);
                utpTransport.Listen(host.ServerPort);

                _compositeTransport = new CompositeTransport();
                _compositeTransport.AddTransport(_directServer);
                _compositeTransport.AddTransport(utpTransport);
                _directServer.Listen(0);
                _server.StartWithTransport(_compositeTransport);

                context.Register(directClient);
            }
            else if (context.Config is SessionConfig.DedicatedServer ds)
            {
                // UTP only (no local player) — create transport explicitly for bridge pump access
                NetworkDriverWrapper utpTransport = new(logger);
                utpTransport.Listen(ds.ServerPort);
                _realTransport = utpTransport;
                _server.StartWithTransport(utpTransport);
            }

            // Build server game loop
            ChunkManager chunkManager = context.Get<ChunkManager>();
            TickRegistry tickRegistry = context.Get<TickRegistry>();

            // Server-private physics manager (not registered in context — client creates its own)
            PlayerPhysicsManager physicsManager = new(
                chunkManager, context.Content.NativeStateRegistry);
            PhysicsSettings physics = context.App.Settings.Physics;
            ChunkSettings chunkSettings = context.App.Settings.Chunk;

            ChunkDirtyTracker dirtyTracker = new();
            chunkManager.OnBlockChanged += dirtyTracker.OnBlockChanged;

            ServerBlockProcessor blockProcessor = new(
                chunkManager,
                context.Content.StateRegistry,
                context.Content.NativeStateRegistry,
                physics.HandMiningMultiplier,
                logger);

            ServerSimulation serverSim = new(
                physicsManager, tickRegistry, physics,
                blockProcessor,
                () => 0f); // TimeOfDay wired in PostInitialize

            ServerChunkProvider chunkProvider = new(chunkManager, context.Content.NativeStateRegistry);

            // Clamp spawn radius to LOD1 distance to ensure full-detail meshes in spawn area
            int spawnRadius = math.min(
                chunkSettings.SpawnLoadRadius,
                SchedulingConfig.LOD1Distance(chunkSettings.RenderDistance));

            ChunkStreamingManager streamingManager = new(
                chunkSettings.YLoadMin,
                chunkSettings.YLoadMax,
                spawnRadius,
                chunkProvider,
                logger);

            // Client readiness timeout: 900 ticks = 30 seconds at 30 TPS
            ClientReadinessWaiter readinessWaiter = new(900);

            // Build the bridge infrastructure: creates bridged server, bridged
            // simulation/block-processor/dirty-tracker, pump, and thread runner.
            INetworkTransport pumpTransport = (INetworkTransport)_compositeTransport ?? _realTransport;
            ulong seed = 0;

            if (context.TryGet(out WorldMetadata meta))
            {
                seed = (ulong)meta.Seed;
            }

            ServerThreadBridgeFactory.BuildResult bridgeResult = ServerThreadBridgeFactory.Build(
                pumpTransport,
                serverSim,
                blockProcessor,
                dirtyTracker,
                chunkProvider,
                streamingManager,
                readinessWaiter,
                contentHash,
                maxPlayers,
                seed,
                () => serverSim.GetTimeOfDay(),
                logger);

            _serverGameLoop = bridgeResult.ServerGameLoop;
            MainThreadBridgePump pump = bridgeResult.Pump;
            _runner = bridgeResult.Runner;

            // For SP/Host, set up local chunk streaming strategy for zero-copy chunk delivery.
            if (context.Config is SessionConfig.Singleplayer or SessionConfig.Host)
            {
                LocalChunkStreamingStrategy localStrategy = new(
                    chunkProvider,
                    null,
                    null);

                // Pre-register strategy for the local peer. ConnectionId(1) is
                // deterministic: DirectTransport is added first to CompositeTransport,
                // and the first Connect event always gets composite ID 1.
                _serverGameLoop.SetPeerStrategy(new ConnectionId(1), localStrategy);

                // ConnectionId(1) is the local peer (DirectTransport added first).
                // Mark it as local and defer the spawn teleport to the main thread
                // since Transform access requires the main thread.
                ConnectionId localConnectionId = new(1);
                MainThreadBridgePump pumpRef = pump;
                _serverGameLoop.OnPlayerAcceptedCallback = (peer, spawnPos) =>
                {
                    if (peer.ConnectionId.Equals(localConnectionId))
                    {
                        peer.IsLocal = true;

                        // Defer teleport to main thread — Transform.position requires main thread
                        float3 pos = spawnPos;
                        pumpRef.EnqueueMainThreadAction(() =>
                        {
                            if (_playerHolder != null)
                            {
                                _playerHolder.Transform.position =
                                    new UnityEngine.Vector3(pos.x, pos.y, pos.z);
                            }
                        });
                    }
                };

                context.Register(localStrategy);
                context.Register(chunkProvider);
            }

            context.Register(_server);
            context.Register(_serverGameLoop);
            context.Register(dirtyTracker);
            context.Register(pump);
            context.Register(_runner);

            if (_compositeTransport != null)
            {
                context.Register(_compositeTransport);
            }
        }

        /// <summary>Wires player transform, LAN broadcaster, network metrics, and starts the server thread.</summary>
        public void PostInitialize(SessionContext context)
        {
            // Capture player transform for the accept callback's spawn teleport.
            // PostInitialize runs after all subsystems are initialized, so PlayerSubsystem
            // has already registered the holder.
            if (context.TryGet(out PlayerTransformHolder player))
            {
                _playerHolder = player;
            }

            // Wire player count to LAN broadcaster if present
            if (context.TryGet(out LanBroadcaster lan))
            {
                _serverGameLoop.OnPlayerCountChanged = count => lan.UpdatePlayerCount(count + 1);
            }

            // Wire network metrics for the debug overlay
            if (context.TryGet(out MetricsRegistry metricsRegistry))
            {
                metricsRegistry.SetNetworkMetrics(_server);
            }

            // Start the background server thread after all wiring is complete
            _runner?.Start();
        }

        /// <summary>Stops the server thread before in-flight jobs are completed.</summary>
        public void Shutdown()
        {
            _runner?.Dispose();
            _runner = null;
        }

        /// <summary>Disposes the network server and all transport layers.</summary>
        public void Dispose()
        {
            if (_server != null)
            {
                // NetworkServer.Dispose() internally disposes the transport it was started with.
                // Null out our references first to avoid double-dispose.
                _compositeTransport = null;
                _realTransport = null;

                _server.Dispose();
                _server = null;
            }

            _directServer?.Dispose();
            _directServer = null;
        }
    }
}
