using System;
using System.Collections.Generic;

using Lithforge.Network;
using Lithforge.Network.Client;
using Lithforge.Network.Message;
using Lithforge.Network.Transport;
using Lithforge.Runtime.Network;
using Lithforge.Runtime.Player;
using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;

using UnityEngine;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class NetworkClientSubsystem : IGameSubsystem
    {
        private NetworkClient _client;

        public string Name
        {
            get { return "NetworkClient"; }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerPhysicsSubsystem),
            typeof(TickRegistrySubsystem),
            typeof(PlayerSubsystem),
            typeof(NetworkServerSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            // Always-server: all rendering modes use a NetworkClient
            return config is SessionConfig.Singleplayer
                or SessionConfig.Host
                or SessionConfig.Client;
        }

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

        public void PostInitialize(SessionContext context)
        {
            // Defer ClientWorldSimulation creation until handshake completes.
            // For DirectTransport (SP/Host), the handshake takes ~2 frames.
            // For UTP (Client), it takes a full network round-trip.
            // LocalPlayerId and ServerTickAtHandshake are only valid after handshake.
            PlayerPhysicsManager physicsManager = context.Get<PlayerPhysicsManager>();
            TickRegistry tickRegistry = context.Get<TickRegistry>();
            InputSnapshotBuilder input = context.TryGet(out InputSnapshotBuilder b) ? b : null;

            _client.OnHandshakeComplete = () =>
            {
                ClientWorldSimulation clientSim = new(
                    tickRegistry, physicsManager, input,
                    _client, _client.LocalPlayerId,
                    _client.ServerTickAtHandshake);

                // Register player state handler for reconciliation
                _client.Dispatcher.RegisterHandler(
                    MessageType.PlayerState, clientSim.OnPlayerStateReceived);

                // Register as the world simulation
                context.Register<IWorldSimulation>(clientSim);
                context.Register(clientSim);

                // Wire into the running GameLoopPoco
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

        public void Shutdown()
        {
        }

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
