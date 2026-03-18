using System;
using System.Collections.Generic;

using Lithforge.Runtime.Debug;
using Lithforge.Runtime.World;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class F3OverlaySubsystem : IGameSubsystem
    {
        public string Name
        {
            get
            {
                return "F3Overlay";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(MetricsSubsystem),
            typeof(PlayerSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        public void Initialize(SessionContext context)
        {
            MetricsRegistry metricsRegistry = context.Get<MetricsRegistry>();
            PlayerTransformHolder player = context.Get<PlayerTransformHolder>();
            PanelSettings panelSettings = SessionInitArgsHolder.Current?.PanelSettings;
            MonoBehaviour host = context.App.CoroutineHost;

            // Chunk border renderer
            ChunkBorderRenderer chunkBorderRenderer =
                host.gameObject.AddComponent<ChunkBorderRenderer>();
            chunkBorderRenderer.Initialize(
                metricsRegistry,
                player.MainCamera,
                context.App.Settings.Debug.ChunkBorderRadius);
            chunkBorderRenderer.SetVisible(false);

            // F3 debug overlay
            F3DebugOverlay debugOverlay = host.gameObject.AddComponent<F3DebugOverlay>();
            debugOverlay.Initialize(
                metricsRegistry,
                chunkBorderRenderer,
                context.App.Settings.Debug,
                panelSettings,
                context.App.FrameProfiler,
                context.App.PipelineStats);

            context.Register(debugOverlay);
            context.Register(chunkBorderRenderer);
        }

        public void PostInitialize(SessionContext context)
        {
        }

        public void Shutdown()
        {
        }

        public void Dispose()
        {
            // MonoBehaviours cleaned up by bootstrap GO destruction
        }
    }
}
