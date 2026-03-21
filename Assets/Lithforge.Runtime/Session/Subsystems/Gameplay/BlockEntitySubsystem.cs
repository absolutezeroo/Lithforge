using System;
using System.Collections.Generic;

using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the block entity tick scheduler for furnaces, chests, etc.</summary>
    public sealed class BlockEntitySubsystem : IGameSubsystem
    {
        /// <summary>The owned block entity tick scheduler.</summary>
        private BlockEntityTickScheduler _scheduler;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "BlockEntity";
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

        /// <summary>Creates the block entity tick scheduler from chunk manager and registries.</summary>
        public void Initialize(SessionContext context)
        {
            ChunkManager chunkManager = context.Get<ChunkManager>();

            _scheduler = new BlockEntityTickScheduler(
                chunkManager,
                context.Content.BlockEntityRegistry,
                context.Content.StateRegistry);

            context.Register(_scheduler);
        }

        /// <summary>Wires the tick adapter and chunk entity load events to the scheduler.</summary>
        public void PostInitialize(SessionContext context)
        {
            // Wire to tick registry
            if (context.TryGet(out TickRegistry registry))
            {
                registry.Register(new BlockEntityTickAdapter(_scheduler));
            }

            // Wire to generation scheduler
            if (context.TryGet(out GenerationScheduler genScheduler))
            {
                genScheduler.OnChunkEntitiesLoaded += _scheduler.RegisterEntitiesForChunk;
            }
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
