namespace Lithforge.Voxel.Chunk
{
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
