using System;

namespace Lithforge.Runtime.Network
{
    /// <summary>
    /// A discovered LAN server, combining the broadcast payload with the
    /// sender's IP address and a timestamp for expiry tracking.
    /// </summary>
    public sealed class LanServerEntry
    {
        /// <summary>IP address of the broadcasting server.</summary>
        public string Address { get; }

        /// <summary>Broadcast payload data.</summary>
        public LanServerInfo Info { get; }

        /// <summary>UTC time when this entry was last seen.</summary>
        public DateTime LastSeen { get; set; }

        /// <summary>Creates an entry with the given address, info payload, and timestamp.</summary>
        public LanServerEntry(string address, LanServerInfo info, DateTime lastSeen)
        {
            Address = address;
            Info = info;
            LastSeen = lastSeen;
        }
    }
}
