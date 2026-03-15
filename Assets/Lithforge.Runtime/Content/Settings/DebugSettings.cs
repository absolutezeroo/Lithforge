using Lithforge.Runtime.Debug.Benchmark;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Settings
{
    [CreateAssetMenu(fileName = "DebugSettings", menuName = "Lithforge/Settings/Debug", order = 4)]
    public sealed class DebugSettings : ScriptableObject
    {
        [Header("Overlay")]
        [Tooltip("Show debug overlay on startup")]
        [SerializeField] private bool showDebugOverlay;

        [Tooltip("Show chunk borders")]
        [SerializeField] private bool showChunkBorders;

        [Header("Logging")]
        [Tooltip("Enable verbose content loading logs")]
        [SerializeField] private bool verboseContentLoading;

        [Tooltip("Enable performance profiling markers")]
        [SerializeField] private bool enableProfiling = true;

        [Header("FPS Smoothing")]
        [Tooltip("EMA alpha for FPS smoothing (0=no smoothing, 1=instant)")]
        [Range(0.01f, 1f)]
        [SerializeField] private float fpsAlpha = 0.1f;

        [Header("Benchmark")]
        [Tooltip("Default benchmark scenario asset (triggered by F5)")]
        [SerializeField] private BenchmarkScenario defaultBenchmarkScenario;

        [Header("Chunk Borders")]
        [Tooltip("Radius in chunks for chunk border wireframes")]
        [Min(1)]
        [SerializeField] private int chunkBorderRadius = 4;

        public bool ShowDebugOverlay
        {
            get { return showDebugOverlay; }
        }

        public bool ShowChunkBorders
        {
            get { return showChunkBorders; }
        }

        public bool VerboseContentLoading
        {
            get { return verboseContentLoading; }
        }

        public bool EnableProfiling
        {
            get { return enableProfiling; }
        }

        public float FpsAlpha
        {
            get { return fpsAlpha; }
        }

        public BenchmarkScenario DefaultBenchmarkScenario
        {
            get { return defaultBenchmarkScenario; }
        }

        public int ChunkBorderRadius
        {
            get { return chunkBorderRadius; }
        }
    }
}
