using System;
using System.Collections.Generic;

using Lithforge.Runtime.Debug;
using Lithforge.Runtime.World;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the F3 debug overlay and chunk border renderer.</summary>
    public sealed class F3OverlaySubsystem : IGameSubsystem
    {
        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "F3Overlay";
            }
        }

        /// <summary>Depends on metrics and player for overlay data sources.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(MetricsSubsystem),
            typeof(PlayerSubsystem),
        };

        /// <summary>Only created for sessions that render.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>Creates the chunk border renderer and F3 overlay on the bootstrap GameObject.</summary>
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

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>MonoBehaviours cleaned up by bootstrap GameObject destruction.</summary>
        public void Dispose()
        {
            // MonoBehaviours cleaned up by bootstrap GO destruction
        }
    }
}
