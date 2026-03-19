using System;
using System.Collections.Generic;

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
        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get { return "WorldSimulation"; }
        }

        /// <summary>No dependencies.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = Array.Empty<Type>();

        /// <summary>Always returns false; all modes now use ClientWorldSimulation.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            // Disabled: all modes now use ClientWorldSimulation via NetworkClientSubsystem
            return false;
        }

        /// <summary>No-op; subsystem is disabled.</summary>
        public void Initialize(SessionContext context)
        {
        }

        /// <summary>No-op; subsystem is disabled.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No-op; subsystem is disabled.</summary>
        public void Shutdown()
        {
        }

        /// <summary>No-op; subsystem is disabled.</summary>
        public void Dispose()
        {
        }
    }
}
