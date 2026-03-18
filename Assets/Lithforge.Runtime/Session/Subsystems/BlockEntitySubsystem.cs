using System;
using System.Collections.Generic;

using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class BlockEntitySubsystem : IGameSubsystem
    {
        private BlockEntityTickScheduler _scheduler;

        public string Name
        {
            get
            {
                return "BlockEntity";
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

            _scheduler = new BlockEntityTickScheduler(
                chunkManager,
                context.Content.BlockEntityRegistry,
                context.Content.StateRegistry);

            context.Register(_scheduler);
        }

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

        public void Shutdown()
        {
        }

        public void Dispose()
        {
        }
    }
}
