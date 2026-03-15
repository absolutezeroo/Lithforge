namespace Lithforge.Runtime.UI
{
    /// <summary>
    /// Phases of the save-to-title process, displayed by SavingScreen.
    /// </summary>
    public enum SaveState
    {
        /// <summary>Completing in-flight generation/mesh jobs.</summary>
        CompletingJobs,

        /// <summary>Serializing dirty chunks to region file caches.</summary>
        SavingChunks,

        /// <summary>Flushing dirty region files to disk.</summary>
        FlushingRegions,

        /// <summary>Save complete.</summary>
        Done,
    }
}
