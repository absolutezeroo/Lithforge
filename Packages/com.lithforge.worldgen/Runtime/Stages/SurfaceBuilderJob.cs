using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Stages
{
    [BurstCompile]
    public struct SurfaceBuilderJob : IJob
    {
        public NativeArray<StateId> ChunkData;

        [ReadOnly] public NativeArray<int> HeightMap;
        [ReadOnly] public int3 ChunkCoord;
        [ReadOnly] public int SeaLevel;
        [ReadOnly] public StateId GrassId;
        [ReadOnly] public StateId DirtId;
        [ReadOnly] public StateId StoneId;
        [ReadOnly] public StateId AirId;

        private const int _dirtDepth = 3;

        public void Execute()
        {
            int chunkWorldY = ChunkCoord.y * ChunkConstants.Size;

            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                for (int x = 0; x < ChunkConstants.Size; x++)
                {
                    int columnIndex = z * ChunkConstants.Size + x;
                    int surfaceY = HeightMap[columnIndex];

                    for (int y = ChunkConstants.Size - 1; y >= 0; y--)
                    {
                        int worldY = chunkWorldY + y;
                        int index = Lithforge.Voxel.Chunk.ChunkData.GetIndex(x, y, z);
                        StateId current = ChunkData[index];

                        if (!current.Equals(StoneId))
                        {
                            continue;
                        }

                        int depth = surfaceY - worldY;

                        if (depth == 1)
                        {
                            if (surfaceY >= SeaLevel)
                            {
                                ChunkData[index] = GrassId;
                            }
                            else
                            {
                                ChunkData[index] = DirtId;
                            }
                        }
                        else if (depth > 1 && depth <= _dirtDepth + 1)
                        {
                            ChunkData[index] = DirtId;
                        }
                    }
                }
            }
        }
    }
}
