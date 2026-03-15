namespace Lithforge.Voxel.Storage
{
    public sealed class WorldScanEntry
    {
        public string DirectoryPath { get; set; }
        public string DirectoryName { get; set; }
        public WorldMetadata Metadata { get; set; }
        public bool IsLocked { get; set; }

        public WorldScanEntry(string directoryPath, string directoryName, WorldMetadata metadata, bool isLocked)
        {
            DirectoryPath = directoryPath;
            DirectoryName = directoryName;
            Metadata = metadata;
            IsLocked = isLocked;
        }
    }
}
