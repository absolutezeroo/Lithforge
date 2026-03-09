namespace Lithforge.Runtime.Spawn
{
    /// <summary>
    /// Phases of the spawn loading process.
    /// </summary>
    public enum SpawnState
    {
        /// <summary>
        /// Waiting for spawn-area chunks to reach ChunkState.Ready.
        /// </summary>
        Checking,

        /// <summary>
        /// All chunks ready; scanning downward to find a safe Y position.
        /// </summary>
        FindingY,

        /// <summary>
        /// Safe Y found; teleporting player to the spawn position.
        /// </summary>
        Teleporting,

        /// <summary>
        /// Spawn complete; player is live.
        /// </summary>
        Done,
    }
}
