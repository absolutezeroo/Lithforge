namespace Lithforge.Voxel.Storage
{
    /// <summary>
    ///     Result entry from <see cref="WorldDirectoryScanner.ScanWorlds"/>.
    ///     Contains the directory path, parsed metadata, and session lock status for one world.
    /// </summary>
    public sealed class WorldScanEntry
    {
        /// <summary>Full filesystem path to the world directory.</summary>
        public string DirectoryPath { get; set; }

        /// <summary>Directory name (last path segment), used as the fallback display name.</summary>
        public string DirectoryName { get; set; }

        /// <summary>Parsed world.json metadata, or null if the file is missing or unreadable.</summary>
        public WorldMetadata Metadata { get; set; }

        /// <summary>Whether the world is currently locked by another process.</summary>
        public bool IsLocked { get; set; }

        /// <summary>Creates a scan entry with all fields populated.</summary>
        public WorldScanEntry(string directoryPath, string directoryName, WorldMetadata metadata, bool isLocked)
        {
            DirectoryPath = directoryPath;
            DirectoryName = directoryName;
            Metadata = metadata;
            IsLocked = isLocked;
        }
    }
}
