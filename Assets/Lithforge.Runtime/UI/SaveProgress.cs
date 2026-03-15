namespace Lithforge.Runtime.UI
{
    /// <summary>
    /// Snapshot of save progress, pushed to SavingScreen each frame.
    /// Value type — no allocation on access.
    /// </summary>
    public struct SaveProgress
    {
        /// <summary>Current phase of the save process.</summary>
        public SaveState Phase;

        /// <summary>Total number of dirty chunks to save.</summary>
        public int TotalChunks;

        /// <summary>Number of chunks saved so far.</summary>
        public int SavedChunks;

        /// <summary>Total number of dirty regions to flush.</summary>
        public int TotalRegions;

        /// <summary>Number of regions flushed so far.</summary>
        public int FlushedRegions;
    }
}
