using System;
using System.Collections.Generic;

using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class MetricsSubsystem : IGameSubsystem
    {
        public string Name
        {
            get
            {
                return "Metrics";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
            typeof(ChunkMeshStoreSubsystem),
            typeof(ChunkPoolSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        public void Initialize(SessionContext context)
        {
            PlayerTransformHolder player = context.Get<PlayerTransformHolder>();
            ChunkManager chunkManager = context.Get<ChunkManager>();
            ChunkMeshStore meshStore = context.Get<ChunkMeshStore>();
            ChunkPool chunkPool = context.Get<ChunkPool>();

            MetricsRegistry metricsRegistry = new();
            // Note: GameLoop reference will be null at this point — use PostInitialize
            metricsRegistry.Initialize(
                chunkManager,
                meshStore,
                chunkPool,
                player.Controller,
                player.MainCamera,
                null, // GameLoop ref set later
                context.App.FrameProfiler,
                context.App.PipelineStats,
                context.App.Settings.Debug.FpsAlpha);

            context.Register(metricsRegistry);
        }

        public void PostInitialize(SessionContext context)
        {
        }

        public void Shutdown()
        {
        }

        public void Dispose()
        {
        }
    }
}
