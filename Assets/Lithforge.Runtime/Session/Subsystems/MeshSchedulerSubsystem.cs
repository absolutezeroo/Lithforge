using System;
using System.Collections.Generic;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the mesh scheduler for Burst greedy mesh generation at LOD0.</summary>
    public sealed class MeshSchedulerSubsystem : IGameSubsystem
    {
        /// <summary>The owned frustum culling instance.</summary>
        private ChunkCulling _culling;

        /// <summary>The owned mesh scheduler instance.</summary>
        private MeshScheduler _scheduler;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "MeshScheduler";
            }
        }

        /// <summary>Depends on chunk manager and mesh store for mesh generation pipeline.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkManagerSubsystem),
            typeof(ChunkMeshStoreSubsystem),
        };

        /// <summary>Only created for sessions that render chunks.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>Creates the mesh scheduler and chunk culling from configured budgets.</summary>
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

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>Completes all in-flight mesh jobs before shutdown.</summary>
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
