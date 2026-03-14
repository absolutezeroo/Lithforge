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

        [Header("FPS Sampling")]
        [Tooltip("FPS averaging interval in seconds")]
        [Min(0.05f)]
        [SerializeField] private float fpsSampleInterval = 0.5f;

        [Header("Benchmark")]
        [Tooltip("Fly speed during automated benchmark (blocks/sec)")]
        [Min(1f)]
        [SerializeField] private float benchmarkFlySpeed = 50f;

        [Tooltip("Duration of automated benchmark in seconds")]
        [Min(1f)]
        [SerializeField] private float benchmarkDuration = 10f;

        [Header("Overlay Appearance")]
        [Tooltip("Background panel alpha (0=transparent, 1=opaque)")]
        [Range(0f, 1f)]
        [SerializeField] private float overlayBackgroundAlpha = 0.6f;

        [Tooltip("Minimum font size for overlay labels")]
        [Min(8)]
        [SerializeField] private int overlayMinFontSize = 18;

        [Tooltip("Screen height divisor for dynamic font sizing")]
        [Min(10)]
        [SerializeField] private int overlayScreenDivisor = 50;

        [Tooltip("Width of the overlay panel in pixels")]
        [Min(100)]
        [SerializeField] private int overlayPanelWidth = 420;

        [Tooltip("Padding inside the overlay panel in pixels")]
        [Min(0)]
        [SerializeField] private int overlayPadding = 8;

        [Tooltip("Additional vertical spacing between lines in pixels")]
        [Min(0)]
        [SerializeField] private int overlayLineSpacing = 6;

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

        public float FpsSampleInterval
        {
            get { return fpsSampleInterval; }
        }

        public float OverlayBackgroundAlpha
        {
            get { return overlayBackgroundAlpha; }
        }

        public int OverlayMinFontSize
        {
            get { return overlayMinFontSize; }
        }

        public int OverlayScreenDivisor
        {
            get { return overlayScreenDivisor; }
        }

        public int OverlayPanelWidth
        {
            get { return overlayPanelWidth; }
        }

        public int OverlayPadding
        {
            get { return overlayPadding; }
        }

        public int OverlayLineSpacing
        {
            get { return overlayLineSpacing; }
        }

        public float BenchmarkFlySpeed
        {
            get { return benchmarkFlySpeed; }
        }

        public float BenchmarkDuration
        {
            get { return benchmarkDuration; }
        }
    }
}
