namespace Lithforge.Runtime
{
    /// <summary>
    /// Top-level application states that gate GameLoop behavior.
    /// </summary>
    public enum GameState
    {
        /// <summary>World selection UI is shown. Game systems are not running.</summary>
        Title = 0,

        /// <summary>
        /// Full freeze: tick loop and job scheduling are suspended.
        /// Cursor is unlocked. Pause menu is visible.
        /// In-flight jobs continue to drain via PollCompleted.
        /// </summary>
        PausedFull = 1,

        /// <summary>
        /// Overlay-only pause: world simulation continues (ticks, gen, mesh).
        /// Cursor is unlocked. Pause menu is visible.
        /// For future multiplayer or creative-mode use.
        /// </summary>
        PausedOverlay = 2,

        /// <summary>Normal in-game state. All systems run. Cursor is locked.</summary>
        Playing = 3,
    }
}
