using Lithforge.Voxel.Storage;

namespace Lithforge.Runtime.World
{
    /// <summary>
    /// Immutable value object carrying the parameters for a single game session.
    /// Created by <see cref="WorldSelectionScreen"/> and passed directly into
    /// <see cref="Bootstrap.LithforgeBootstrap.RunGameSession"/>.
    /// Replaces the static WorldLauncher mailbox pattern.
    /// </summary>
    public sealed class WorldSession
    {
        /// <summary>Absolute filesystem path to the selected world directory.</summary>
        public string WorldPath { get; }

        /// <summary>Human-readable world name shown in UI and metadata.</summary>
        public string DisplayName { get; }

        /// <summary>World generation seed.</summary>
        public long Seed { get; }

        /// <summary>Survival or Creative mode for this world.</summary>
        public GameMode GameMode { get; }

        /// <summary>True when the player just created this world (triggers starting-item grant).</summary>
        public bool IsNewWorld { get; }

        public WorldSession(
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
    }
}
