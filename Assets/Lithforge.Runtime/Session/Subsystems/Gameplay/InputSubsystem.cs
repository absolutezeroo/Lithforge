using System;
using System.Collections.Generic;

using Lithforge.Runtime.World;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>
    ///     Placeholder subsystem for <see cref="InputSnapshotBuilder" />.
    ///     The actual builder is created and registered by <see cref="PlayerSubsystem" />
    ///     because it requires the player and camera transforms.
    /// </summary>
    public sealed class InputSubsystem : IGameSubsystem
    {
        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "Input";
            }
        }

        /// <summary>Depends on player subsystem which creates the input builder.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
        };

        /// <summary>Only created for sessions that render.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>No-op; InputSnapshotBuilder is created by PlayerSubsystem.</summary>
        public void Initialize(SessionContext context)
        {
            // InputSnapshotBuilder is created and registered by PlayerSubsystem.
        }

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>No owned disposable resources.</summary>
        public void Dispose()
        {
        }
    }
}
