using Lithforge.Voxel.Block;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    /// Burst-compiled job that extracts a 32x32 border slice from chunk data for
    /// cross-chunk meshing. Replaces the managed ChunkBorderExtractor.ExtractBorder
    /// call so that border extraction runs on worker threads instead of blocking
    /// the main thread.
    ///
    /// Face direction mapping (same as ChunkBorderExtractor):
    ///   0 = +X (East):  x=31 plane, u=z, v=y
    ///   1 = -X (West):  x=0  plane, u=z, v=y
    ///   2 = +Y (Up):    y=31 plane, u=x, v=z
    ///   3 = -Y (Down):  y=0  plane, u=x, v=z
    ///   4 = +Z (South): z=31 plane, u=x, v=y
    ///   5 = -Z (North): z=0  plane, u=x, v=y
    ///
    /// Output layout: output[v * 32 + u].
    /// </summary>
    [BurstCompile]
    public struct ExtractSingleBorderJob : IJob
    {
        [ReadOnly] public NativeArray<StateId> ChunkData;
        public int FaceDirection;
        [WriteOnly] public NativeArray<StateId> Output;

        public void Execute()
        {
            int size = ChunkConstants.Size;
            int lastIdx = size - 1;

            for (int v = 0; v < size; v++)
            {
                for (int u = 0; u < size; u++)
                {
                    int srcIndex;

                    switch (FaceDirection)
                    {
                        case 0: // +X: x=31, u=z, v=y
                            srcIndex = Chunk.ChunkData.GetIndex(lastIdx, v, u);
                            break;
                        case 1: // -X: x=0, u=z, v=y
                            srcIndex = Chunk.ChunkData.GetIndex(0, v, u);
                            break;
                        case 2: // +Y: y=31, u=x, v=z
                            srcIndex = Chunk.ChunkData.GetIndex(u, lastIdx, v);
                            break;
                        case 3: // -Y: y=0, u=x, v=z
                            srcIndex = Chunk.ChunkData.GetIndex(u, 0, v);
                            break;
                        case 4: // +Z: z=31, u=x, v=y
                            srcIndex = Chunk.ChunkData.GetIndex(u, v, lastIdx);
                            break;
                        default: // -Z: z=0, u=x, v=y
                            srcIndex = Chunk.ChunkData.GetIndex(u, v, 0);
                            break;
                    }

                    Output[v * size + u] = ChunkData[srcIndex];
                }
            }
        }
    }
}
