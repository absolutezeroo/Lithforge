using System;

namespace Lithforge.Voxel.Storage
{
    /// <summary>
    ///     Represents a single ban record for a player, stored in admin/bans.json.
    /// </summary>
    public sealed class BanEntry
    {
        /// <summary>The banned player's UUID.</summary>
        public string PlayerUuid { get; set; } = "";

        /// <summary>The IP address that was banned (may be empty if not IP-banned).</summary>
        public string IpAddress { get; set; } = "";

        /// <summary>Human-readable reason for the ban.</summary>
        public string Reason { get; set; } = "";

        /// <summary>UUID of the admin who issued the ban.</summary>
        public string BannedBy { get; set; } = "";

        /// <summary>UTC timestamp when the ban was created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>UTC timestamp when the ban expires, or null for permanent bans.</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>Returns true if this ban has expired based on the current UTC time.</summary>
        public bool IsExpired
        {
            get { return ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value; }
        }
    }
}
