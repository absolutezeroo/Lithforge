using System;
using System.Collections.Generic;

using Lithforge.Network;
using Lithforge.Network.Client;
using Lithforge.Network.Message;
using Lithforge.Network.Transport;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Identity;
using Lithforge.Runtime.Network;
using Lithforge.Runtime.Player;
using Lithforge.Runtime.Session;
using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using Unity.Mathematics;

using UnityEngine;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>
    ///     Subsystem that creates the network client and wires client-side world simulation.
    ///     Connects via DirectTransport for SP/Host or UTP for remote Client mode.
    /// </summary>
    public sealed class NetworkClientSubsystem : IGameSubsystem
    {
        /// <summary>The owned network client instance.</summary>
        private NetworkClient _client;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get { return "NetworkClient"; }
        }

        /// <summary>Depends on tick, player, server, and chunk manager subsystems for simulation wiring.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(TickRegistrySubsystem),
            typeof(PlayerSubsystem),
            typeof(NetworkServerSubsystem),
            typeof(ChunkManagerSubsystem),
        };

        /// <summary>Created for all rendering modes (SP, Host, Client).</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            // Always-server: all rendering modes use a NetworkClient
            return config is SessionConfig.Singleplayer
                or SessionConfig.Host
                or SessionConfig.Client;
        }

        /// <summary>Creates the network client and connects via DirectTransport or UTP.</summary>
        public void Initialize(SessionContext context)
        {
            ILogger logger = context.App.Logger;
            ContentHash contentHash = ContentHashComputer.Compute(context.Content.StateRegistry);

            string playerName = context.Config switch
            {
                SessionConfig.Client client => client.PlayerName,
                _ => "Player",
            };

            _client = new NetworkClient(logger, contentHash, playerName);

            // Wire player identity for challenge-response authentication.
            // SP/Host uses a fixed "local" UUID with no public key (DirectTransport skips challenge).
            // Remote clients load or generate their persistent ECDSA keypair.
            if (context.Config is SessionConfig.Singleplayer or SessionConfig.Host)
            {
                _client.SetIdentity(
                    PlayerIdentity.LocalUuid, Array.Empty<byte>(), _ => Array.Empty<byte>());
            }
            else if (context.Config is SessionConfig.Client)
            {
                PlayerIdentity identity = new();
                identity.LoadOrGenerate(logger);

                if (identity.IsValid)
                {
                    _client.SetIdentity(identity.Uuid, identity.PublicKeyBytes, identity.Sign);
                }
                else
                {
                    // Identity generation failed — use a transient UUID without auth.
                    // This ensures the handshake always carries a player UUID, even if
                    // the persistent identity store is unavailable (e.g. read-only filesystem).
                    string fallbackUuid = Guid.NewGuid().ToString();
                    logger.LogWarning(
                        $"[Identity] Identity unavailable, using transient UUID: {fallbackUuid}");
                    _client.SetIdentity(
                        fallbackUuid, Array.Empty<byte>(), _ => Array.Empty<byte>());
                }

                context.Register(identity);
            }

            if (context.Config is SessionConfig.Singleplayer or SessionConfig.Host)
            {
                // Connect via DirectTransport (server is in the same process)
                DirectTransportClient directClient = context.Get<DirectTransportClient>();
                _client.ConnectDirect(directClient, Time.realtimeSinceStartup);
            }
            else if (context.Config is SessionConfig.Client clientConfig)
            {
                // Connect via UTP to remote server
                _client.Connect(
                    clientConfig.ServerAddress,
                    clientConfig.ServerPort,
                    Time.realtimeSinceStartup);
            }

            context.Register(_client);
            context.Register<INetworkClient>(_client);
        }

        /// <summary>Wires network metrics, creates client-private physics manager, and defers simulation until handshake.</summary>
        public void PostInitialize(SessionContext context)
        {
            // Wire network metrics for client-only mode (SP/Host use the server as source)
            if (context.Config is SessionConfig.Client
                && context.TryGet(out MetricsRegistry metricsRegistry))
            {
                metricsRegistry.SetNetworkMetrics(_client);
            }

            // Create client-private physics manager (not shared with server)
            ChunkManager chunkManager = context.Get<ChunkManager>();
            ThreadSafeChunkReader clientChunkReader = new(chunkManager);
            PlayerPhysicsManager clientPhysicsManager = new(
                clientChunkReader, context.Content.NativeStateRegistry);

            PhysicsSettings physics = context.App.Settings.Physics;

            // Defer ClientWorldSimulation creation until handshake completes.
            // For DirectTransport (SP/Host), the handshake takes ~2 frames.
            // For UTP (Client), it takes a full network round-trip.
            // LocalPlayerId and ServerTickAtHandshake are only valid after handshake.
            TickRegistry tickRegistry = context.Get<TickRegistry>();
            InputSnapshotBuilder input = context.TryGet(out InputSnapshotBuilder b) ? b : null;

            // Register body factory — invoked by ClientChunkHandlerSubsystem on SpawnInit
            ClientPlayerBodyFactory bodyFactory = new(spawnPosition =>
            {
                ushort localId = _client.LocalPlayerId;

                // Create the client-side physics body
                PlayerPhysicsBody clientBody = clientPhysicsManager.AddPlayer(
                    localId, spawnPosition, physics);

                // Wire to PlayerTransformHolder, PlayerController, GameLoopPoco
                if (context.TryGet(out PlayerTransformHolder playerHolder))
                {
                    playerHolder.PhysicsBody = clientBody;

                    if (playerHolder.Controller != null)
                    {
                        playerHolder.Controller.SetPhysicsBody(clientBody);
                    }
                }

                if (context.TryGet(out GameLoopPoco gameLoop))
                {
                    gameLoop.SetPlayerPhysicsBody(clientBody);
                }

                // Wire collision override from ClientBlockPredictor (if available)
                if (context.TryGet(out ClientBlockPredictor predictor))
                {
                    clientBody.SetCollisionOverride(coord =>
                    {
                        if (predictor.TryGetOriginalState(coord, out StateId originalState))
                        {
                            return originalState;
                        }

                        return null;
                    });
                }
            });

            context.Register(bodyFactory);

            _client.OnHandshakeComplete = () =>
            {
                ushort localId = _client.LocalPlayerId;

                // Server is local when using DirectTransport (SP/Host).
                // Physics always runs on the client; TickRegistry is gated to avoid double-tick.
                bool serverIsLocal = context.Config is SessionConfig.Singleplayer
                    or SessionConfig.Host;

                ClientWorldSimulation clientSim = new(
                    tickRegistry, clientPhysicsManager, input,
                    _client, localId,
                    _client.ServerTickAtHandshake,
                    serverIsLocal,
                    context.App.Logger);

                // Register player state handler for reconciliation
                _client.Dispatcher.RegisterHandler(
                    MessageType.PlayerState, clientSim.OnPlayerStateReceived);

                // Register as the world simulation
                context.Register<IWorldSimulation>(clientSim);
                context.Register(clientSim);

                // Wire into the running GameLoopPoco (body wiring deferred to body factory)
                if (context.TryGet(out GameLoopPoco gameLoop))
                {
                    gameLoop.SetWorldSimulation(clientSim);
                }

                // Wire remote player handler now that we have a valid LocalPlayerId
                if (context.TryGet(out RemotePlayerManager manager))
                {
                    ClientRemotePlayerHandler handler = new(
                        manager, _client, _client.LocalPlayerId);

                    context.Register(handler);
                    clientSim.SetRemotePlayerStateHandler(handler.OnRemotePlayerState);
                }
            };
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>Disconnects and disposes the network client.</summary>
        public void Dispose()
        {
            if (_client != null)
            {
                _client.Disconnect();
                _client.Dispose();
                _client = null;
            }
        }
    }
}
