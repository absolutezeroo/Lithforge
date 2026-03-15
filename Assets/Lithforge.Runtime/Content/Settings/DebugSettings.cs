using Lithforge.Runtime.Debug.Benchmark;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Settings
{
    /// <summary>
    /// Toggles and tuning knobs for the debug overlay (F3), chunk borders, profiling, and benchmarks.
    /// </summary>
    /// <remarks>Loaded from <c>Resources/Settings/DebugSettings</c>.</remarks>
    [CreateAssetMenu(fileName = "DebugSettings", menuName = "Lithforge/Settings/Debug", order = 4)]
    public sealed class DebugSettings : ScriptableObject
    {
        /// <summary>Whether the F3 debug overlay is visible when the game first starts.</summary>
        [Header("Overlay")]
        [Tooltip("Show debug overlay on startup")]
        [SerializeField] private bool showDebugOverlay;

        /// <summary>Whether chunk border wireframes (F3+G) render on startup.</summary>
        [Tooltip("Show chunk borders")]
        [SerializeField] private bool showChunkBorders;

        /// <summary>Emits per-phase ContentPipeline log messages when enabled.</summary>
        [Header("Logging")]
        [Tooltip("Enable verbose content loading logs")]
        [SerializeField] private bool verboseContentLoading;

        /// <summary>Activates Unity ProfilerMarker instrumentation for FrameProfiler sections.</summary>
        [Tooltip("Enable performance profiling markers")]
        [SerializeField] private bool enableProfiling = true;

        /// <summary>Exponential moving average alpha for the FPS counter; lower values smooth more.</summary>
        [Header("FPS Smoothing")]
        [Tooltip("EMA alpha for FPS smoothing (0=no smoothing, 1=instant)")]
        [Range(0.01f, 1f)]
        [SerializeField] private float fpsAlpha = 0.1f;

        /// <summary>Scenario asset that runs when the user presses F5 without selecting a specific benchmark.</summary>
        [Header("Benchmark")]
        [Tooltip("Default benchmark scenario asset (triggered by F5)")]
        [SerializeField] private BenchmarkScenario defaultBenchmarkScenario;

        /// <summary>How many chunks outward from the camera to draw chunk border wireframes.</summary>
        [Header("Chunk Borders")]
        [Tooltip("Radius in chunks for chunk border wireframes")]
        [Min(1)]
        [SerializeField] private int chunkBorderRadius = 4;

        /// <inheritdoc cref="showDebugOverlay"/>
        public bool ShowDebugOverlay
        {
            get { return showDebugOverlay; }
        }

        /// <inheritdoc cref="showChunkBorders"/>
        public bool ShowChunkBorders
        {
            get { return showChunkBorders; }
        }

        /// <inheritdoc cref="verboseContentLoading"/>
        public bool VerboseContentLoading
        {
            get { return verboseContentLoading; }
        }

        /// <inheritdoc cref="enableProfiling"/>
        public bool EnableProfiling
        {
            get { return enableProfiling; }
        }

        /// <inheritdoc cref="fpsAlpha"/>
        public float FpsAlpha
        {
            get { return fpsAlpha; }
        }

        /// <inheritdoc cref="defaultBenchmarkScenario"/>
        public BenchmarkScenario DefaultBenchmarkScenario
        {
            get { return defaultBenchmarkScenario; }
        }

        /// <inheritdoc cref="chunkBorderRadius"/>
        public int ChunkBorderRadius
        {
            get { return chunkBorderRadius; }
        }
    }
}
