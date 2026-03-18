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
    public sealed class GenerationSchedulerSubsystem : IGameSubsystem
    {
        private GenerationScheduler _scheduler;

        public string Name
        {
            get
            {
                return "GenerationScheduler";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkManagerSubsystem),
            typeof(ChunkMeshStoreSubsystem),
            typeof(WorldGenSubsystem),
            typeof(DecorationSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.HasLocalWorld && config is not SessionConfig.Client;
        }

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
        }

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

        public void Shutdown()
        {
            _scheduler?.Shutdown();
        }

        public void Dispose()
        {
            // GenerationScheduler doesn't implement IDisposable; nothing to dispose.
        }
    }
}
