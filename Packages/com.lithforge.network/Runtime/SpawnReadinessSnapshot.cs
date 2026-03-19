namespace Lithforge.Network
{
    /// <summary>
    ///     Snapshot of chunk readiness within a spawn volume. Used by
    ///     <see cref="Client.ClientReadinessTracker" /> to report progress
    ///     to the loading screen.
    /// </summary>
    public struct SpawnReadinessSnapshot
    {
        /// <summary>Total number of chunks in the spawn volume.</summary>
        public int TotalChunks;

        /// <summary>Number of chunks that are ready.</summary>
        public int ReadyChunks;

        /// <summary>True when all chunks in the volume are ready.</summary>
        public bool IsComplete;
    }
}
