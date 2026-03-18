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
        public string WorldPath { get; }

        public string DisplayName { get; }

        public long Seed { get; }

        public GameMode GameMode { get; }

        public bool IsNewWorld { get; }
    }
}
