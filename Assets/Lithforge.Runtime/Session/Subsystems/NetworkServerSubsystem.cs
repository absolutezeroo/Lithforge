using System;
using System.Collections.Generic;

using Lithforge.Network;
using Lithforge.Network.Chunk;
using Lithforge.Network.Server;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Network;
using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class NetworkServerSubsystem : IGameSubsystem
    {
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
            return config is SessionConfig.Host or SessionConfig.DedicatedServer;
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

            ushort port = context.Config switch
            {
                SessionConfig.Host host => host.ServerPort,
                SessionConfig.DedicatedServer ds => ds.ServerPort,
                _ => 7777,
            };

            _server = new NetworkServer(logger, contentHash, maxPlayers);

            if (context.TryGet(out WorldMetadata metadata))
            {
                _server.WorldSeed = (ulong)metadata.Seed;
            }

            _server.Start(port);

            // Build server game loop
            ChunkManager chunkManager = context.Get<ChunkManager>();
            PlayerPhysicsManager physicsManager = context.Get<PlayerPhysicsManager>();
            TickRegistry tickRegistry = context.Get<TickRegistry>();
            PhysicsSettings physics = context.App.Settings.Physics;

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

            ServerChunkProvider chunkProvider = new(chunkManager);

            ChunkStreamingManager streamingManager = new(
                context.App.Settings.Chunk.YLoadMin,
                context.App.Settings.Chunk.YLoadMax,
                3, logger);

            _serverGameLoop = new ServerGameLoop(
                _server, serverSim, blockProcessor, chunkProvider,
                dirtyTracker, streamingManager, logger);

            context.Register(_server);
            context.Register(_serverGameLoop);
            context.Register(dirtyTracker);
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
        }
    }
}
