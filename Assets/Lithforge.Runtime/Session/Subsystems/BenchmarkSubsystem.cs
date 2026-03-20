using System;
using System.Collections.Generic;

using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Debug.Benchmark;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the benchmark runner for automated performance testing.</summary>
    public sealed class BenchmarkSubsystem : IGameSubsystem
    {
        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "Benchmark";
            }
        }

        /// <summary>Depends on metrics, player, and block interaction for benchmark commands.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(MetricsSubsystem),
            typeof(PlayerSubsystem),
            typeof(BlockInteractionSubsystem),
        };

        /// <summary>Only created for sessions that render.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>Creates the benchmark runner with context, metrics, and player references.</summary>
        public void Initialize(SessionContext context)
        {
            MetricsRegistry metricsRegistry = context.Get<MetricsRegistry>();
            PlayerTransformHolder player = context.Get<PlayerTransformHolder>();
            BlockInteraction blockInteraction = context.Get<BlockInteraction>();
            ChunkManager chunkManager = context.Get<ChunkManager>();
            PanelSettings panelSettings = SessionInitArgsHolder.Current?.PanelSettings;
            MonoBehaviour host = context.App.CoroutineHost;

            BenchmarkContext benchmarkContext = new()
            {
                Metrics = metricsRegistry,
                ChunkManager = chunkManager,
                PlayerController = player.Controller,
                PlayerTransform = player.Transform,
                MainCamera = player.MainCamera,
                GameLoopPoco = null, // Set in PostInitialize after SessionBridgeSubsystem
                BlockInteraction = blockInteraction,
                Logger = context.App.Logger,
            };

            BenchmarkRunner benchmarkRunner = host.gameObject.AddComponent<BenchmarkRunner>();
            benchmarkRunner.Initialize(
                benchmarkContext,
                context.App.Settings.Debug,
                metricsRegistry,
                player.Controller,
                panelSettings,
                context.App.FrameProfiler,
                context.App.PipelineStats);

            context.Register(benchmarkRunner);
        }

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>MonoBehaviour cleaned up by bootstrap GameObject destruction.</summary>
        public void Dispose()
        {
            // MonoBehaviour cleaned up by bootstrap GO destruction
        }
    }
}
