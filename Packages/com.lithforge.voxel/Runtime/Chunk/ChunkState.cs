namespace Lithforge.Voxel.Chunk
{
    public enum ChunkState
    {
        Unloaded,
        Loading,
        Generating,
        Decorating,
        Generated,
        Meshing,
        Ready,
    }
}
