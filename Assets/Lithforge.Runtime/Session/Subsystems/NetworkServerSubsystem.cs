using System;
using System.Collections.Generic;

using Lithforge.Network;
using Lithforge.Network.Chunk;
using Lithforge.Network.Server;
using Lithforge.Network.Transport;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Network;
using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.Spawn;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;

using Unity.Mathematics;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class NetworkServerSubsystem : IGameSubsystem
    {
        private CompositeTransport _compositeTransport;

        private DirectTransportServer _directServer;

        private NetworkServer _server;

        private ServerGameLoop _serverGameLoop;

        public string Name
        {
            get
            {
                return "NetworkServer";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkManagerSubsystem),
            typeof(PlayerPhysicsSubsystem),
            typeof(TickRegistrySubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            // Always-server: singleplayer now runs a server too
            return config is SessionConfig.Singleplayer
                or SessionConfig.Host
                or SessionConfig.DedicatedServer;
        }

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
                // UTP only (no local player)
                _server.Start(ds.ServerPort);
            }

            // Build server game loop
            ChunkManager chunkManager = context.Get<ChunkManager>();
            PlayerPhysicsManager physicsManager = context.Get<PlayerPhysicsManager>();
            TickRegistry tickRegistry = context.Get<TickRegistry>();
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

            // Default streaming strategy: network serialization
            NetworkChunkStreamingStrategy networkStrategy = new(_server, chunkProvider);

            _serverGameLoop = new ServerGameLoop(
                _server, serverSim, blockProcessor, chunkProvider,
                dirtyTracker, streamingManager, networkStrategy, logger);

            // For SP/Host, set up local chunk streaming strategy and spawn loading tracker.
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

                // Create SpawnLoadingTracker so the LoadingScreen can poll meshing progress.
                // Spawn chunk is updated when OnPlayerAccepted fires for the local peer.
                SpawnLoadingTracker spawnTracker = new(
                    chunkProvider,
                    int3.zero,
                    spawnRadius,
                    chunkSettings.YLoadMin,
                    chunkSettings.YLoadMax);

                // ConnectionId(1) is the local peer (DirectTransport added first).
                // Mark it as local and update the spawn tracker when accepted.
                ConnectionId localConnectionId = new(1);
                _serverGameLoop.OnPlayerAcceptedCallback = (peer, spawnPos) =>
                {
                    if (peer.ConnectionId.Equals(localConnectionId))
                    {
                        peer.IsLocal = true;

                        int3 spawnChunk = new(
                            (int)math.floor(spawnPos.x / ChunkConstants.Size),
                            (int)math.floor(spawnPos.y / ChunkConstants.Size),
                            (int)math.floor(spawnPos.z / ChunkConstants.Size));
                        spawnTracker.UpdateSpawnChunk(spawnChunk);
                    }
                };

                context.Register(localStrategy);
                context.Register(chunkProvider);
                context.Register(spawnTracker);
            }

            context.Register(_server);
            context.Register(_serverGameLoop);
            context.Register(dirtyTracker);

            if (_compositeTransport != null)
            {
                context.Register(_compositeTransport);
            }
        }

        public void PostInitialize(SessionContext context)
        {
            // Wire player count to LAN broadcaster if present
            if (context.TryGet(out LanBroadcaster lan))
            {
                _serverGameLoop.OnPlayerCountChanged = count => lan.UpdatePlayerCount(count + 1);
            }
        }

        public void Shutdown()
        {
        }

        public void Dispose()
        {
            if (_server != null)
            {
                _server.Dispose();
                _server = null;
            }

            _compositeTransport?.Dispose();
            _compositeTransport = null;

            _directServer?.Dispose();
            _directServer = null;
        }
    }
}
