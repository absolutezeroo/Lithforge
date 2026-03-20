using System;

using Lithforge.Network.Chunk;
using Lithforge.Network.Server;
using Lithforge.Network.Transport;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     Factory that constructs all bridge infrastructure and returns the two public
    ///     components needed by the runtime tier: <see cref="MainThreadBridgePump" /> and
    ///     <see cref="ServerThreadRunner" />. All internal bridge types are hidden behind
    ///     this factory to maintain encapsulation.
    /// </summary>
    public static class ServerThreadBridgeFactory
    {
        /// <summary>
        ///     Result of building the bridge infrastructure. Contains all objects
        ///     needed by the subsystem layer to wire everything together.
        /// </summary>
        public sealed class BuildResult
        {
            /// <summary>The server game loop, created with bridged implementations.</summary>
            public ServerGameLoop ServerGameLoop { get; internal set; }

            /// <summary>The bridged NetworkServer instance (for strategies, callbacks, etc.).</summary>
            public NetworkServer BridgedServer { get; internal set; }

            /// <summary>Main-thread bridge pump for servicing the background server thread.</summary>
            public MainThreadBridgePump Pump { get; internal set; }

            /// <summary>Background server thread runner.</summary>
            public ServerThreadRunner Runner { get; internal set; }
        }

        /// <summary>
        ///     Constructs the full bridge infrastructure. Creates a bridged NetworkServer,
        ///     bridged simulation/block-processor/dirty-tracker, and wires them into a
        ///     ServerGameLoop that will run on a background thread.
        /// </summary>
        public static BuildResult Build(
            INetworkTransport realTransport,
            IServerSimulation realSimulation,
            IServerBlockProcessor realBlockProcessor,
            ChunkDirtyTracker realDirtyTracker,
            IServerChunkProvider chunkProvider,
            ChunkStreamingManager streamingManager,
            ClientReadinessWaiter readinessWaiter,
            ContentHash contentHash,
            int maxPlayers,
            ulong worldSeed,
            Func<float> timeOfDayProvider,
            ILogger logger)
        {
            ServerThreadBridge bridge = new();

            BridgedTransport bridgedTransport = new(bridge);
            BridgedSimulation bridgedSimulation = new(bridge, realSimulation);
            BridgedBlockProcessor bridgedBlockProcessor = new(bridge);
            BridgedDirtyTracker bridgedDirtyTracker = new(bridge);

            // Create a second NetworkServer using the bridged transport.
            // This server instance runs on the server thread and sees events
            // through the bridge rather than directly from UTP.
            NetworkServer bridgedServer = new(logger, contentHash, maxPlayers);
            bridgedServer.WorldSeed = worldSeed;
            bridgedServer.StartWithTransport(bridgedTransport);

            // Default streaming strategy uses the bridged server (runs on server thread)
            NetworkChunkStreamingStrategy networkStrategy = new(bridgedServer, chunkProvider);

            ServerGameLoop serverGameLoop = new(
                bridgedServer, bridgedSimulation, bridgedBlockProcessor, chunkProvider,
                bridgedDirtyTracker, streamingManager, networkStrategy, readinessWaiter,
                bridge, logger);

            MainThreadBridgePump pump = new(
                bridge,
                realTransport,
                realSimulation,
                realBlockProcessor,
                realDirtyTracker,
                timeOfDayProvider);

            ServerThreadRunner runner = new(serverGameLoop, bridge, logger);

            return new BuildResult
            {
                ServerGameLoop = serverGameLoop,
                BridgedServer = bridgedServer,
                Pump = pump,
                Runner = runner,
            };
        }
    }
}
