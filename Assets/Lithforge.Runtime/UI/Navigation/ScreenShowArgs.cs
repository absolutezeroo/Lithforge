namespace Lithforge.Runtime.UI.Navigation
{
    /// <summary>
    /// Parameter object passed to <see cref="IScreen.OnShow"/> when a screen
    /// becomes the topmost screen in the stack. Carries context about why the
    /// screen is being shown (initial push vs. returning from a popped screen).
    /// </summary>
    public sealed class ScreenShowArgs
    {
        /// <summary>
        /// True when this screen is being shown because the screen above it
        /// was popped (back-navigation). False on a fresh push.
        /// </summary>
        public bool IsReturning { get; }

        /// <summary>
        /// Optional data passed from the screen that triggered the push.
        /// May be null. Screens should cast to the expected type.
        /// </summary>
        public object Context { get; }

        /// <summary>Creates a new ScreenShowArgs with the given returning state and optional context data.</summary>
        public ScreenShowArgs(bool isReturning, object context = null)
        {
            IsReturning = isReturning;
            Context = context;
        }
    }
}
