using Lithforge.Voxel.Storage;

namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    ///     Context object passed from <see cref="WorldSelectionScreen" /> to
    ///     <see cref="HostSettingsModal" /> via <see cref="Navigation.ScreenShowArgs.Context" />.
    ///     Carries the selected world's data so HostSettingsModal can build
    ///     a <see cref="World.SessionConfig.Host" /> without re-reading disk.
    /// </summary>
    public sealed class HostWorldContext
    {
        /// <summary>Creates a host world context with the given world metadata.</summary>
        public HostWorldContext(
            string worldPath,
            string displayName,
            long seed,
            GameMode gameMode,
            bool isNewWorld)
        {
            WorldPath = worldPath;
            DisplayName = displayName;
            Seed = seed;
            GameMode = gameMode;
            IsNewWorld = isNewWorld;
        }
        /// <summary>Absolute file path to the world directory.</summary>
        public string WorldPath { get; }

        /// <summary>Human-readable world name shown in the UI.</summary>
        public string DisplayName { get; }

        /// <summary>World generation seed.</summary>
        public long Seed { get; }

        /// <summary>Game mode (Survival, Creative) for the world.</summary>
        public GameMode GameMode { get; }

        /// <summary>True if this is a newly created world that has not been saved yet.</summary>
        public bool IsNewWorld { get; }
    }
}
