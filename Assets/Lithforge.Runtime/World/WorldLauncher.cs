using Lithforge.Voxel.Storage;

namespace Lithforge.Runtime.World
{
    /// <summary>
    /// Static mailbox that carries the user's world selection from
    /// <see cref="WorldSelectionScreen"/> into <see cref="Bootstrap.LithforgeBootstrap"/>.
    /// Set once before the game session starts, then cleared on return to title.
    /// </summary>
    public static class WorldLauncher
    {
        /// <summary>Absolute filesystem path to the selected world directory.</summary>
        public static string SelectedWorldPath { get; private set; }
        /// <summary>Human-readable world name shown in UI and metadata.</summary>
        public static string SelectedDisplayName { get; private set; }
        /// <summary>World generation seed.</summary>
        public static long SelectedSeed { get; private set; }
        /// <summary>Survival or Creative mode for this world.</summary>
        public static GameMode SelectedGameMode { get; private set; }
        /// <summary>True when the player just created this world (triggers starting-item grant instead of save restore).</summary>
        public static bool IsNewWorld { get; private set; }

        /// <summary>
        /// Stores all parameters needed to launch a game session.
        /// Called by <see cref="WorldSelectionScreen"/> before it destroys itself.
        /// </summary>
        /// <param name="path">Absolute path to the world save directory.</param>
        /// <param name="displayName">User-facing world name.</param>
        /// <param name="seed">World generation seed.</param>
        /// <param name="mode">Survival or Creative.</param>
        /// <param name="isNew">True if the world was just created and has no saved player state.</param>
        public static void SetWorld(string path, string displayName, long seed, GameMode mode, bool isNew)
        {
            SelectedWorldPath = path;
            SelectedDisplayName = displayName;
            SelectedSeed = seed;
            SelectedGameMode = mode;
            IsNewWorld = isNew;
        }

        /// <summary>
        /// Resets all fields to defaults. Called after a session ends so the next
        /// world-selection cycle starts clean.
        /// </summary>
        public static void Clear()
        {
            SelectedWorldPath = null;
            SelectedDisplayName = null;
            SelectedSeed = 0;
            SelectedGameMode = GameMode.Survival;
            IsNewWorld = false;
        }
    }
}
