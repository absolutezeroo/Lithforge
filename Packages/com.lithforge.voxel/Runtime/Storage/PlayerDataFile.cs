namespace Lithforge.Voxel.Storage
{
    /// <summary>
    ///     JSON-serializable wrapper for per-player save data.
    ///     Stored in playerdata/&lt;uuid&gt;.json with format versioning.
    /// </summary>
    public sealed class PlayerDataFile
    {
        /// <summary>Current format version for player data files.</summary>
        public const int CurrentFormatVersion = 1;

        /// <summary>The player's unique identifier (UUID string).</summary>
        public string Uuid { get; set; } = "";

        /// <summary>Format version of this player data file for future migration.</summary>
        public int FormatVersion { get; set; } = CurrentFormatVersion;

        /// <summary>The player's serialized state (position, rotation, inventory, time of day).</summary>
        public WorldPlayerState State { get; set; }
    }
}
