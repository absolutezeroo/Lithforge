using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    [CreateAssetMenu(fileName = "DebugSettings", menuName = "Lithforge/Settings/Debug", order = 4)]
    public sealed class DebugSettings : ScriptableObject
    {
        [Header("Overlay")]
        [Tooltip("Show debug overlay on startup")]
        [SerializeField] private bool _showDebugOverlay;

        [Tooltip("Show chunk borders")]
        [SerializeField] private bool _showChunkBorders;

        [Header("Logging")]
        [Tooltip("Enable verbose content loading logs")]
        [SerializeField] private bool _verboseContentLoading;

        [Tooltip("Enable performance profiling markers")]
        [SerializeField] private bool _enableProfiling = true;

        [Header("FPS Sampling")]
        [Tooltip("FPS averaging interval in seconds")]
        [Min(0.05f)]
        [SerializeField] private float _fpsSampleInterval = 0.5f;

        [Header("Overlay Appearance")]
        [Tooltip("Background panel alpha (0=transparent, 1=opaque)")]
        [Range(0f, 1f)]
        [SerializeField] private float _overlayBackgroundAlpha = 0.6f;

        [Tooltip("Minimum font size for overlay labels")]
        [Min(8)]
        [SerializeField] private int _overlayMinFontSize = 18;

        [Tooltip("Screen height divisor for dynamic font sizing")]
        [Min(10)]
        [SerializeField] private int _overlayScreenDivisor = 50;

        [Tooltip("Width of the overlay panel in pixels")]
        [Min(100)]
        [SerializeField] private int _overlayPanelWidth = 420;

        [Tooltip("Padding inside the overlay panel in pixels")]
        [Min(0)]
        [SerializeField] private int _overlayPadding = 8;

        [Tooltip("Additional vertical spacing between lines in pixels")]
        [Min(0)]
        [SerializeField] private int _overlayLineSpacing = 6;

        public bool ShowDebugOverlay
        {
            get { return _showDebugOverlay; }
        }

        public bool ShowChunkBorders
        {
            get { return _showChunkBorders; }
        }

        public bool VerboseContentLoading
        {
            get { return _verboseContentLoading; }
        }

        public bool EnableProfiling
        {
            get { return _enableProfiling; }
        }

        public float FpsSampleInterval
        {
            get { return _fpsSampleInterval; }
        }

        public float OverlayBackgroundAlpha
        {
            get { return _overlayBackgroundAlpha; }
        }

        public int OverlayMinFontSize
        {
            get { return _overlayMinFontSize; }
        }

        public int OverlayScreenDivisor
        {
            get { return _overlayScreenDivisor; }
        }

        public int OverlayPanelWidth
        {
            get { return _overlayPanelWidth; }
        }

        public int OverlayPadding
        {
            get { return _overlayPadding; }
        }

        public int OverlayLineSpacing
        {
            get { return _overlayLineSpacing; }
        }
    }
}
