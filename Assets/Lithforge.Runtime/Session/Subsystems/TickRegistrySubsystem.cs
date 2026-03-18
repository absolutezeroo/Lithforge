using System;
using System.Collections.Generic;

using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class TickRegistrySubsystem : IGameSubsystem
    {
        public string Name
        {
            get
            {
                return "TickRegistry";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = Array.Empty<Type>();

        public bool ShouldCreate(SessionConfig config)
        {
            return true;
        }

        public void Initialize(SessionContext context)
        {
            TickRegistry registry = new();
            context.Register(registry);
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
