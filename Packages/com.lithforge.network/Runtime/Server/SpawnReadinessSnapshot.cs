namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Snapshot of chunk readiness within a spawn volume. Used to drive
    ///     the Loading→Playing gate and the loading screen progress bar.
    ///     A chunk counts as ready if it is at ChunkState.Ready (meshed) or
    ///     is confirmed all-air (no mesh needed).
    /// </summary>
    public struct SpawnReadinessSnapshot
    {
        /// <summary>Total number of chunks in the spawn volume.</summary>
        public int TotalChunks;

        /// <summary>Number of chunks that are ready (meshed or all-air).</summary>
        public int ReadyChunks;

        /// <summary>True when all chunks in the volume are ready.</summary>
        public bool IsComplete;
    }
}
