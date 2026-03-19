using System;
using System.Collections.Generic;

using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the fixed-tick registry for registering tick adapters.</summary>
    public sealed class TickRegistrySubsystem : IGameSubsystem
    {
        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "TickRegistry";
            }
        }

        /// <summary>No dependencies.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = Array.Empty<Type>();

        /// <summary>Always created for all session types.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return true;
        }

        /// <summary>Creates and registers a new TickRegistry.</summary>
        public void Initialize(SessionContext context)
        {
            TickRegistry registry = new();
            context.Register(registry);
        }

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>No owned resources to dispose.</summary>
        public void Dispose()
        {
        }
    }
}
