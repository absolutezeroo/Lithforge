using System;

namespace Lithforge.Runtime.Network
{
    /// <summary>
    /// Payload data broadcast by a LAN server and displayed in the JoinGame
    /// screen's LAN discovery list. Serialized as JSON inside a
    /// <see cref="LanDiscoveryPacket"/>.
    /// </summary>
    [Serializable]
    public sealed class LanServerInfo
    {
        /// <summary>Human-readable server/world name.</summary>
        public string serverName;

        /// <summary>The UTP game port (not the discovery port).</summary>
        public ushort gamePort;

        /// <summary>Number of players currently connected.</summary>
        public int playerCount;

        /// <summary>Maximum number of players allowed.</summary>
        public int maxPlayers;

        /// <summary>Game version string for compatibility display.</summary>
        public string gameVersion;

        /// <summary>Content hash for compatibility checking.</summary>
        public string contentHash;

        /// <summary>World display name.</summary>
        public string worldName;

        /// <summary>Game mode (Survival/Creative).</summary>
        public string gameMode;
    }
}
