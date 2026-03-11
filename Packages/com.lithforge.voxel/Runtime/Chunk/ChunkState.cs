namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    /// Chunk lifecycle states. The numeric order matters — code uses ordinal
    /// comparisons (>=, &lt;) to check readiness. States from RelightPending onward
    /// have valid voxel data. States from Generated onward are eligible for meshing.
    /// </summary>
    public enum ChunkState
    {
        Unloaded,
        Loading,
        Generating,
        Decorating,
        /// <summary>
        /// Block was edited and light needs recalculation before remeshing.
        /// Transitions to Generated once relighting is complete.
        /// </summary>
        RelightPending,
        Generated,
        Meshing,
        Ready,
    }
}
