using System;
using System.Collections.Generic;

using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class RelightSchedulerSubsystem : IGameSubsystem
    {
        private RelightScheduler _scheduler;

        public string Name
        {
            get
            {
                return "RelightScheduler";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkManagerSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return true;
        }

        public void Initialize(SessionContext context)
        {
            ChunkManager chunkManager = context.Get<ChunkManager>();

            _scheduler = new RelightScheduler(
                chunkManager,
                context.Content.NativeStateRegistry);

            context.Register(_scheduler);
        }

        public void PostInitialize(SessionContext context)
        {
        }

        public void Shutdown()
        {
            _scheduler?.Shutdown();
        }

        public void Dispose()
        {
        }
    }
}
