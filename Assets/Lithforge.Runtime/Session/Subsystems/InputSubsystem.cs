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
        public string Name
        {
            get
            {
                return "Input";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        public void Initialize(SessionContext context)
        {
            // InputSnapshotBuilder is created and registered by PlayerSubsystem.
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
