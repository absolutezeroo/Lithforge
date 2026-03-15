using Lithforge.Voxel.Storage;

namespace Lithforge.Runtime.World
{
    public static class WorldLauncher
    {
        public static string SelectedWorldPath { get; private set; }
        public static string SelectedDisplayName { get; private set; }
        public static long SelectedSeed { get; private set; }
        public static GameMode SelectedGameMode { get; private set; }
        public static bool IsNewWorld { get; private set; }

        public static void SetWorld(string path, string displayName, long seed, GameMode mode, bool isNew)
        {
            SelectedWorldPath = path;
            SelectedDisplayName = displayName;
            SelectedSeed = seed;
            SelectedGameMode = mode;
            IsNewWorld = isNew;
        }

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
