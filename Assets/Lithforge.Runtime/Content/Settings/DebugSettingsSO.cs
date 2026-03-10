using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    [CreateAssetMenu(fileName = "DebugSettings", menuName = "Lithforge/Settings/Debug", order = 4)]
    public sealed class DebugSettingsSO : ScriptableObject
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
    }
}
