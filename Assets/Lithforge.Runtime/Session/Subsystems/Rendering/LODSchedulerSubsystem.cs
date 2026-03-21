using System;
using System.Collections.Generic;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the LOD scheduler for downsampled mesh generation at distance.</summary>
    public sealed class LODSchedulerSubsystem : IGameSubsystem
    {
        /// <summary>The owned LOD scheduler instance.</summary>
        private LODScheduler _scheduler;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "LODScheduler";
            }
        }

        /// <summary>Depends on chunk manager, mesh store, and mesh scheduler for LOD pipeline.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkManagerSubsystem),
            typeof(ChunkMeshStoreSubsystem),
            typeof(MeshSchedulerSubsystem),
        };

        /// <summary>Only created for sessions that render chunks.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>Creates the LOD scheduler with distance-based LOD level configuration.</summary>
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

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>Completes all in-flight LOD mesh jobs before shutdown.</summary>
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
