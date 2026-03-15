namespace Lithforge.Runtime.Debug
{
    /// <summary>
    /// Three-state cycle for the F3 debug overlay: Off → Minimal (FPS only) → Full (all panels).
    /// </summary>
    public enum OverlayState
    {
        Off = 0,
        Minimal = 1,
        Full = 2,
    }
}
