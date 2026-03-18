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
    public sealed class BenchmarkSubsystem : IGameSubsystem
    {
        public string Name
        {
            get
            {
                return "Benchmark";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(MetricsSubsystem),
            typeof(PlayerSubsystem),
            typeof(BlockInteractionSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

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

        public void PostInitialize(SessionContext context)
        {
        }

        public void Shutdown()
        {
        }

        public void Dispose()
        {
            // MonoBehaviour cleaned up by bootstrap GO destruction
        }
    }
}
