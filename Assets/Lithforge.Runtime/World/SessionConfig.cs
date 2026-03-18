using Lithforge.Voxel.Storage;

namespace Lithforge.Runtime.World
{
    /// <summary>
    /// Closed discriminated union carrying the parameters for a single game session.
    /// Each variant carries only the fields relevant to its networking role, making
    /// invalid states unrepresentable. Created by menu screens and passed into
    /// <see cref="Bootstrap.LithforgeBootstrap.RunGameSession"/>.
    /// </summary>
    /// <remarks>
    /// The private constructor prevents external subclassing, closing the hierarchy
    /// to exactly four variants. Use pattern matching in the bootstrap to dispatch.
    /// </remarks>
    public abstract record SessionConfig
    {
        private SessionConfig() { }

        /// <summary>True when this session owns a local world directory.</summary>
        public bool HasLocalWorld
        {
            get { return this is Singleplayer or Host or DedicatedServer; }
        }

        /// <summary>True when this session requires rendering, audio, and UI.</summary>
        public bool RequiresRendering
        {
            get { return this is not DedicatedServer; }
        }

        /// <summary>Singleplayer — no networking, full local world.</summary>
        public sealed record Singleplayer(
            string WorldPath,
            string DisplayName,
            long Seed,
            GameMode GameMode,
            bool IsNewWorld) : SessionConfig;

        /// <summary>Host — runs both server and client (listen server) on a local world.</summary>
        public sealed record Host(
            string WorldPath,
            string DisplayName,
            long Seed,
            GameMode GameMode,
            bool IsNewWorld,
            ushort ServerPort,
            int MaxPlayers) : SessionConfig;

        /// <summary>Client — connects to a remote server, no local world.</summary>
        public sealed record Client(
            string ServerAddress,
            ushort ServerPort,
            string PlayerName) : SessionConfig;

        /// <summary>Dedicated server — headless, no local player, no rendering.</summary>
        public sealed record DedicatedServer(
            string WorldPath,
            long Seed,
            ushort ServerPort,
            int MaxPlayers) : SessionConfig;
    }
}
