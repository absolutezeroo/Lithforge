using System;

namespace Lithforge.Runtime.World
{
    /// <summary>
    /// A single saved or recently connected server in the persistent server list.
    /// Serialized to JSON via <see cref="SavedServerList"/>.
    /// </summary>
    [Serializable]
    public sealed class SavedServerEntry
    {
        /// <summary>User-chosen display name for this server.</summary>
        public string name;

        /// <summary>Server IP address or hostname.</summary>
        public string address;

        /// <summary>Server port.</summary>
        public ushort port;

        /// <summary>Player name used when connecting to this server.</summary>
        public string playerName;

        /// <summary>ISO 8601 UTC timestamp of last successful connection.</summary>
        public string lastConnected;

        /// <summary>True if the user has starred this server.</summary>
        public bool favorite;
    }
}
