namespace Lithforge.Runtime.Spawn
{
    /// <summary>
    /// Snapshot of spawn loading progress, read by the loading screen each frame.
    /// Value type — no allocation on access.
    /// </summary>
    public struct SpawnProgress
    {
        /// <summary>Current phase of the spawn process.</summary>
        public SpawnState Phase;

        /// <summary>Total number of chunks in the spawn volume.</summary>
        public int TotalChunks;

        /// <summary>Number of chunks that have reached ChunkState.Ready.</summary>
        public int ReadyChunks;

        /// <summary>World-space X of the chosen spawn position (valid after Done).</summary>
        public int SpawnX;

        /// <summary>World-space Y of the chosen spawn position (valid after Done).</summary>
        public int SpawnY;

        /// <summary>World-space Z of the chosen spawn position (valid after Done).</summary>
        public int SpawnZ;
    }
}
