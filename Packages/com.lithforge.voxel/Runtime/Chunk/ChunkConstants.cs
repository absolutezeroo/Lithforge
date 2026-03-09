namespace Lithforge.Voxel.Chunk
{
    public static class ChunkConstants
    {
        public const int Size = 32;

        public const int SizeSquared = Size * Size;

        public const int Volume = Size * Size * Size;

        public const int SizeBits = 5;

        public const int SizeMask = Size - 1;
    }
}
