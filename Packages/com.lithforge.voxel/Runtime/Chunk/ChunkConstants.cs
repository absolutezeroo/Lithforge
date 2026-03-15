namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    /// Compile-time constants for the cubic chunk dimensions.
    /// All chunks are 32x32x32 voxels; these values are used throughout
    /// indexing, bit masking, and allocation size calculations.
    /// </summary>
    public static class ChunkConstants
    {
        /// <summary>Side length of a chunk in voxels (32).</summary>
        public const int Size = 32;

        /// <summary>Number of voxels in a single 2D slice (Size * Size = 1024).</summary>
        public const int SizeSquared = Size * Size;

        /// <summary>Total voxel count per chunk (Size^3 = 32768).</summary>
        public const int Volume = Size * Size * Size;

        /// <summary>Log2 of Size (5), used for bit-shift division/multiplication.</summary>
        public const int SizeBits = 5;

        /// <summary>Bitmask for local coordinate extraction (Size - 1 = 31).</summary>
        public const int SizeMask = Size - 1;
    }
}
