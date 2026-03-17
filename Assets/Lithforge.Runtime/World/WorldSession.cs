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

        /// <summary>Networking role for this session. Defaults to Singleplayer.</summary>
        public NetworkRole NetworkRole { get; }

        /// <summary>Server address for Client mode. Ignored in other modes.</summary>
        public string ServerAddress { get; }

        /// <summary>Server port. Used by Host/Client/DedicatedServer modes.</summary>
        public ushort ServerPort { get; }

        public WorldSession(
            string worldPath,
            string displayName,
            long seed,
            GameMode gameMode,
            bool isNewWorld,
            NetworkRole networkRole = NetworkRole.Singleplayer,
            string serverAddress = null,
            ushort serverPort = 25565)
        {
            WorldPath = worldPath;
            DisplayName = displayName;
            Seed = seed;
            GameMode = gameMode;
            IsNewWorld = isNewWorld;
            NetworkRole = networkRole;
            ServerAddress = serverAddress;
            ServerPort = serverPort;
        }
    }
}
