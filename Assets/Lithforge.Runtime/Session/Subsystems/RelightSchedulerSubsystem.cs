using System;
using System.Collections.Generic;

using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the relight scheduler for cross-chunk light update jobs.</summary>
    public sealed class RelightSchedulerSubsystem : IGameSubsystem
    {
        /// <summary>The owned relight scheduler instance.</summary>
        private RelightScheduler _scheduler;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "RelightScheduler";
            }
        }

        /// <summary>Depends on chunk manager for chunk data access.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkManagerSubsystem),
        };

        /// <summary>Always created for all session types.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return true;
        }

        /// <summary>Creates the relight scheduler from chunk manager and native state registry.</summary>
        public void Initialize(SessionContext context)
        {
            ChunkManager chunkManager = context.Get<ChunkManager>();

            _scheduler = new RelightScheduler(
                chunkManager,
                context.Content.NativeStateRegistry);

            context.Register(_scheduler);
        }

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>Completes all in-flight relight jobs before shutdown.</summary>
        public void Shutdown()
        {
            _scheduler?.Shutdown();
        }

        /// <summary>No owned disposable resources beyond the scheduler.</summary>
        public void Dispose()
        {
        }
    }
}
