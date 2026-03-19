using System;
using System.Collections.Generic;

using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the metrics registry for collecting debug overlay and benchmark data.</summary>
    public sealed class MetricsSubsystem : IGameSubsystem
    {
        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "Metrics";
            }
        }

        /// <summary>Depends on player, mesh store, and chunk pool for metric data sources.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
            typeof(ChunkMeshStoreSubsystem),
            typeof(ChunkPoolSubsystem),
        };

        /// <summary>Only created for sessions that render.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>Creates and initializes the metrics registry with all data sources.</summary>
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

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
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
