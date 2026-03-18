using System;
using System.Collections.Generic;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class LODSchedulerSubsystem : IGameSubsystem
    {
        private LODScheduler _scheduler;

        public string Name
        {
            get
            {
                return "LODScheduler";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkManagerSubsystem),
            typeof(ChunkMeshStoreSubsystem),
            typeof(MeshSchedulerSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        public void Initialize(SessionContext context)
        {
            ChunkManager chunkManager = context.Get<ChunkManager>();
            ChunkMeshStore meshStore = context.Get<ChunkMeshStore>();
            ChunkCulling culling = context.Get<ChunkCulling>();
            ChunkSettings cs = context.App.Settings.Chunk;
            int rd = cs.RenderDistance;

            _scheduler = new LODScheduler(
                chunkManager,
                context.Content.NativeStateRegistry,
                context.Content.NativeAtlasLookup,
                meshStore,
                culling,
                context.App.PipelineStats,
                SchedulingConfig.MaxLODMeshesPerFrame(rd),
                SchedulingConfig.MaxLODCompletionsPerFrame(rd),
                cs.LodCompletionBudgetMs,
                SchedulingConfig.LOD1Distance(rd),
                SchedulingConfig.LOD2Distance(rd),
                SchedulingConfig.LOD3Distance(rd));

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
