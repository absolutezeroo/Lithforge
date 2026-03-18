using System;
using System.Collections.Generic;

using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>
    ///     Formerly created WorldSimulation for singleplayer/host modes.
    ///     In the always-server architecture, all modes use ClientWorldSimulation created
    ///     by <see cref="NetworkClientSubsystem" />. This subsystem is now disabled.
    /// </summary>
    public sealed class WorldSimulationSubsystem : IGameSubsystem
    {
        public string Name
        {
            get { return "WorldSimulation"; }
        }

        public IReadOnlyList<Type> Dependencies { get; } = Array.Empty<Type>();

        public bool ShouldCreate(SessionConfig config)
        {
            // Disabled: all modes now use ClientWorldSimulation via NetworkClientSubsystem
            return false;
        }

        public void Initialize(SessionContext context)
        {
        }

        public void PostInitialize(SessionContext context)
        {
        }

        public void Shutdown()
        {
        }

        public void Dispose()
        {
        }
    }
}
