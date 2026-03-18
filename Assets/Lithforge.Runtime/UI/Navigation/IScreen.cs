using System;

namespace Lithforge.Runtime.UI.Navigation
{
    /// <summary>
    ///     Contract for all top-level screens managed by <see cref="ScreenManager" />.
    ///     Implemented by MonoBehaviours that own a UIDocument. The ScreenManager
    ///     drives lifecycle transitions; individual screens never manage their own
    ///     visibility or cursor state.
    /// </summary>
    public interface IScreen
    {
        /// <summary>Unique screen name for logging and stack inspection.</summary>
        public string ScreenName { get; }

        /// <summary>
        ///     When true, this screen blocks input from reaching screens below it
        ///     in the stack (e.g., pause menu, inventory). HUD elements like
        ///     CrosshairHUD return false.
        /// </summary>
        public bool IsInputOpaque { get; }

        /// <summary>
        ///     When true, the cursor is unlocked and visible while this screen
        ///     is the topmost opaque screen. When false, cursor remains locked.
        /// </summary>
        public bool RequiresCursor { get; }

        /// <summary>
        ///     Called by ScreenManager when this screen becomes the topmost screen.
        ///     The screen should show its UIDocument root and prepare for input.
        /// </summary>
        public void OnShow(ScreenShowArgs args);

        /// <summary>
        ///     Called by ScreenManager when this screen is being popped or covered.
        ///     The screen should hide its UIDocument root. <paramref name="onComplete" />
        ///     must be invoked once the hide transition finishes (or immediately
        ///     if no transition is used).
        /// </summary>
        public void OnHide(Action onComplete);

        /// <summary>
        ///     Called when the user presses Escape while this screen is topmost
        ///     and <see cref="IsInputOpaque" /> is true. Return true if the screen
        ///     handled the Escape (e.g., by requesting a pop). Return false to
        ///     let the ScreenManager apply default behavior (pop this screen).
        /// </summary>
        public bool HandleEscape();
    }
}
