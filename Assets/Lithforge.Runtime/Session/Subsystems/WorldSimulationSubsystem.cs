using System;
using System.Collections.Generic;

using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>
    ///     Creates the appropriate <see cref="IWorldSimulation" /> implementation
    ///     based on session config (local, client, or host).
    ///     Deferred to PostInitialize because it needs InputSnapshotBuilder and TickRegistry.
    /// </summary>
    public sealed class WorldSimulationSubsystem : IGameSubsystem
    {
        public string Name
        {
            get
            {
                return "WorldSimulation";
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
            return true;
        }

        public void Initialize(SessionContext context)
        {
            // Deferred to PostInitialize — needs InputSnapshotBuilder from PlayerSubsystem.
        }

        public void PostInitialize(SessionContext context)
        {
            PlayerPhysicsManager physicsManager = context.Get<PlayerPhysicsManager>();
            TickRegistry tickRegistry = context.Get<TickRegistry>();

            if (context.Config is not SessionConfig.Client)
            {
                // Singleplayer or Host: local simulation
                InputSnapshotBuilder input = context.TryGet(out InputSnapshotBuilder b) ? b : null;
                WorldSimulation sim = new(tickRegistry, physicsManager, input);
                context.Register<IWorldSimulation>(sim);
            }
            // Client mode simulation is handled by NetworkClientSubsystem.PostInitialize
        }

        public void Shutdown()
        {
        }

        public void Dispose()
        {
        }
    }
}
