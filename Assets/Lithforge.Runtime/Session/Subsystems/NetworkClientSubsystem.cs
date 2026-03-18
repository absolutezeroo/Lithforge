using System;
using System.Collections.Generic;

using Lithforge.Network;
using Lithforge.Network.Client;
using Lithforge.Network.Message;
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
            get
            {
                return "NetworkClient";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerPhysicsSubsystem),
            typeof(TickRegistrySubsystem),
            typeof(PlayerSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config is SessionConfig.Client;
        }

        public void Initialize(SessionContext context)
        {
            SessionConfig.Client clientConfig = (SessionConfig.Client)context.Config;
            ILogger logger = context.App.Logger;
            ContentHash contentHash = ContentHashComputer.Compute(context.Content.StateRegistry);

            _client = new NetworkClient(logger, contentHash, clientConfig.PlayerName);
            _client.Connect(
                clientConfig.ServerAddress,
                clientConfig.ServerPort,
                Time.realtimeSinceStartup);

            context.Register(_client);
            context.Register<INetworkClient>(_client);
        }

        public void PostInitialize(SessionContext context)
        {
            // Create client-side prediction
            PlayerPhysicsManager physicsManager = context.Get<PlayerPhysicsManager>();
            TickRegistry tickRegistry = context.Get<TickRegistry>();

            InputSnapshotBuilder input = context.TryGet(out InputSnapshotBuilder b) ? b : null;

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
