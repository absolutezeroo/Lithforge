using Lithforge.Voxel.Block;
using Unity.Collections;

namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    /// Extracts 32x32 border slices from chunk data for cross-chunk meshing.
    /// Each slice is the outermost face of a chunk in a given direction.
    /// Output layout: output[v * 32 + u] where u,v are face-local axes.
    ///
    /// Face direction mapping:
    ///   0 = +X (East):  x=31 plane, u=z, v=y
    ///   1 = -X (West):  x=0  plane, u=z, v=y
    ///   2 = +Y (Up):    y=31 plane, u=x, v=z
    ///   3 = -Y (Down):  y=0  plane, u=x, v=z
    ///   4 = +Z (South): z=31 plane, u=x, v=y
    ///   5 = -Z (North): z=0  plane, u=x, v=y
    /// </summary>
    public static class ChunkBorderExtractor
    {
        /// <summary>
        /// Extracts the border slice of a chunk facing the given direction.
        /// The output array must be pre-allocated with length ChunkConstants.SizeSquared (1024).
        /// </summary>
        public static void ExtractBorder(
            NativeArray<StateId> chunkData,
            int faceDirection,
            NativeArray<StateId> output)
        {
            for (int v = 0; v < ChunkConstants.Size; v++)
            {
                for (int u = 0; u < ChunkConstants.Size; u++)
                {
                    int srcIndex;

                    switch (faceDirection)
                    {
                        case 0: // +X: x=31, u=z, v=y
                            srcIndex = ChunkData.GetIndex(ChunkConstants.Size - 1, v, u);
                            break;
                        case 1: // -X: x=0, u=z, v=y
                            srcIndex = ChunkData.GetIndex(0, v, u);
                            break;
                        case 2: // +Y: y=31, u=x, v=z
                            srcIndex = ChunkData.GetIndex(u, ChunkConstants.Size - 1, v);
                            break;
                        case 3: // -Y: y=0, u=x, v=z
                            srcIndex = ChunkData.GetIndex(u, 0, v);
                            break;
                        case 4: // +Z: z=31, u=x, v=y
                            srcIndex = ChunkData.GetIndex(u, v, ChunkConstants.Size - 1);
                            break;
                        default: // -Z: z=0, u=x, v=y
                            srcIndex = ChunkData.GetIndex(u, v, 0);
                            break;
                    }

                    output[v * ChunkConstants.Size + u] = chunkData[srcIndex];
                }
            }
        }
    }
}
