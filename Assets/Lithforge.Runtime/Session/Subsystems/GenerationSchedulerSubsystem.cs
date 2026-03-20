using System;
using System.Collections.Generic;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;
using Lithforge.WorldGen.Decoration;
using Lithforge.WorldGen.Pipeline;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the generation scheduler for Burst terrain generation jobs.</summary>
    public sealed class GenerationSchedulerSubsystem : IGameSubsystem
    {
        /// <summary>The owned generation scheduler instance.</summary>
        private GenerationScheduler _scheduler;

        /// <summary>LRU cache of serialized clean chunks to avoid regeneration on reload.</summary>
        private GeneratedChunkCache _generatedChunkCache;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "GenerationScheduler";
            }
        }

        /// <summary>Depends on chunk manager, mesh store, world gen, and decoration for full pipeline.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkManagerSubsystem),
            typeof(ChunkMeshStoreSubsystem),
            typeof(WorldGenSubsystem),
            typeof(DecorationSubsystem),
        };

        /// <summary>Only created for sessions with local world generation (not pure clients).</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.HasLocalWorld && config is not SessionConfig.Client;
        }

        /// <summary>Creates the generation scheduler with world seed, pipeline, and scheduling budgets.</summary>
        public void Initialize(SessionContext context)
        {
            ChunkManager chunkManager = context.Get<ChunkManager>();
            GenerationPipeline pipeline = context.Get<GenerationPipeline>();
            DecorationStage decoration = context.Get<DecorationStage>();
            ChunkSettings cs = context.App.Settings.Chunk;
            int rd = cs.RenderDistance;

            WorldStorage worldStorage = context.TryGet(out WorldStorage ws)
                ? ws : null;

            long seed = 0;

            if (context.TryGet(out WorldMetadata metadata))
            {
                seed = metadata.Seed;
            }

            _scheduler = new GenerationScheduler(
                chunkManager,
                pipeline,
                decoration,
                worldStorage,
                context.Content.NativeStateRegistry,
                context.App.PipelineStats,
                seed,
                SchedulingConfig.MaxGenerationsPerFrame(rd),
                SchedulingConfig.MaxGenCompletionsPerFrame(rd),
                cs.MaxLightUpdatesPerFrame,
                cs.GenCompletionBudgetMs);

            context.Register(_scheduler);

            _generatedChunkCache = new GeneratedChunkCache();
            _scheduler.SetGeneratedChunkCache(_generatedChunkCache);
            context.Register(_generatedChunkCache);
        }

        /// <summary>Wires the biome tint manager, block entity registry, and liquid scheduler.</summary>
        public void PostInitialize(SessionContext context)
        {
            if (context.TryGet(out BiomeTintManager tint))
            {
                _scheduler.SetBiomeTintManager(tint);
            }

            _scheduler.SetBlockEntityRegistry(context.Content.BlockEntityRegistry);

            if (context.TryGet(out LiquidScheduler liquid))
            {
                _scheduler.SetLiquidScheduler(liquid);
            }
        }

        /// <summary>Completes all in-flight generation jobs before shutdown.</summary>
        public void Shutdown()
        {
            _scheduler?.Shutdown();
        }

        /// <summary>No owned disposable resources beyond the scheduler.</summary>
        public void Dispose()
        {
            // GenerationScheduler doesn't implement IDisposable; nothing to dispose.
        }
    }
}
