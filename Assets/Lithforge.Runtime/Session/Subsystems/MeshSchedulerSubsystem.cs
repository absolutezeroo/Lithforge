using System;
using System.Collections.Generic;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class MeshSchedulerSubsystem : IGameSubsystem
    {
        private ChunkCulling _culling;

        private MeshScheduler _scheduler;

        public string Name
        {
            get
            {
                return "MeshScheduler";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkManagerSubsystem),
            typeof(ChunkMeshStoreSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        public void Initialize(SessionContext context)
        {
            ChunkManager chunkManager = context.Get<ChunkManager>();
            ChunkMeshStore meshStore = context.Get<ChunkMeshStore>();
            ChunkSettings cs = context.App.Settings.Chunk;
            int rd = cs.RenderDistance;

            _culling = new ChunkCulling();

            _scheduler = new MeshScheduler(
                chunkManager,
                context.Content.NativeStateRegistry,
                context.Content.NativeAtlasLookup,
                meshStore,
                _culling,
                context.App.PipelineStats,
                SchedulingConfig.MaxMeshesPerFrame(rd),
                SchedulingConfig.MaxMeshCompletionsPerFrame(rd),
                cs.MeshCompletionBudgetMs);
            _scheduler.UpdateConfig(rd);

            context.Register(_scheduler);
            context.Register(_culling);
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
