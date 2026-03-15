namespace Lithforge.Runtime.Content.WorldGen
{
    /// <summary>
    /// Controls the spatial distribution algorithm used by <c>OreGenerationJob</c> when placing ore blocks.
    /// </summary>
    public enum OreType
    {
        /// <summary>Generates a roughly spheroid cluster of ore blocks centered on a random point.</summary>
        Blob = 0,

        /// <summary>Places individual ore blocks at random positions within the height band, with no clustering.</summary>
        Scatter = 1,
    }
}
