namespace Lithforge.Runtime.Debug
{
    /// <summary>
    /// Three-state cycle for the F3 debug overlay: Off → Minimal (FPS only) → Full (all panels).
    /// </summary>
    public enum OverlayState
    {
        /// <summary>Debug overlay is completely hidden.</summary>
        Off = 0,

        /// <summary>Minimal overlay showing only FPS counter.</summary>
        Minimal = 1,

        /// <summary>Full overlay showing all debug panels (FPS, pipeline, profiler, position).</summary>
        Full = 2,
    }
}
