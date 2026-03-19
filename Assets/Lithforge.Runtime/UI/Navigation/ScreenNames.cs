namespace Lithforge.Runtime.UI.Navigation
{
    /// <summary>
    /// String constants for all screen names used with <see cref="ScreenManager"/>.
    /// Centralizes screen identifiers to avoid magic strings.
    /// </summary>
    public static class ScreenNames
    {
        /// <summary>Main menu screen shown at application startup.</summary>
        public const string MainMenu = "MainMenu";

        /// <summary>World selection screen for choosing or creating worlds.</summary>
        public const string WorldSelection = "WorldSelection";

        /// <summary>Join game screen for connecting to a remote server.</summary>
        public const string JoinGame = "JoinGame";

        /// <summary>Host settings modal for configuring LAN game parameters.</summary>
        public const string HostSettings = "HostSettings";

        /// <summary>Connection progress screen shown while connecting to a server.</summary>
        public const string ConnectionProgress = "ConnectionProgress";

        /// <summary>Loading screen shown during world generation and spawn chunk loading.</summary>
        public const string Loading = "Loading";

        /// <summary>Saving screen shown during save-to-title process.</summary>
        public const string Saving = "Saving";

        /// <summary>Pause menu screen shown when Escape is pressed during gameplay.</summary>
        public const string Pause = "Pause";

        /// <summary>Settings screen for graphics, gameplay, and audio options.</summary>
        public const string Settings = "Settings";

        /// <summary>Player inventory screen with crafting grid.</summary>
        public const string PlayerInventory = "PlayerInventory";

        /// <summary>Crosshair HUD overlay at screen center.</summary>
        public const string Crosshair = "Crosshair";

        /// <summary>Hotbar display at the bottom of the screen.</summary>
        public const string Hotbar = "Hotbar";

        /// <summary>F3 debug overlay showing performance and world metrics.</summary>
        public const string DebugOverlay = "DebugOverlay";
    }
}
