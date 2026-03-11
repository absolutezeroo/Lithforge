using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Noise;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Stages
{
    [BurstCompile]
    public struct TerrainShapeJob : IJob
    {
        public NativeArray<StateId> ChunkData;
        public NativeArray<int> HeightMap;

        [ReadOnly] public long Seed;
        [ReadOnly] public int3 ChunkCoord;
        [ReadOnly] public NativeNoiseConfig NoiseConfig;
        [ReadOnly] public int SeaLevel;
        [ReadOnly] public StateId StoneId;
        [ReadOnly] public StateId WaterId;
        [ReadOnly] public StateId AirId;

        public void Execute()
        {
            int chunkWorldX = ChunkCoord.x * ChunkConstants.Size;
            int chunkWorldY = ChunkCoord.y * ChunkConstants.Size;
            int chunkWorldZ = ChunkCoord.z * ChunkConstants.Size;

            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                for (int x = 0; x < ChunkConstants.Size; x++)
                {
                    float worldX = chunkWorldX + x;
                    float worldZ = chunkWorldZ + z;

                    float noiseValue = NativeNoise.Sample2D(worldX, worldZ, NoiseConfig, Seed);
                    int surfaceY = SeaLevel + (int)math.round(noiseValue);

                    int columnIndex = z * ChunkConstants.Size + x;
                    HeightMap[columnIndex] = surfaceY;

                    for (int y = 0; y < ChunkConstants.Size; y++)
                    {
                        int worldY = chunkWorldY + y;
                        int index = Lithforge.Voxel.Chunk.ChunkData.GetIndex(x, y, z);

                        if (worldY <= surfaceY)
                        {
                            ChunkData[index] = StoneId;
                        }
                        else if (worldY < SeaLevel)
                        {
                            ChunkData[index] = WaterId;
                        }
                        else
                        {
                            ChunkData[index] = AirId;
                        }
                    }
                }
            }

            UpdateHeightMap(chunkWorldY);
        }

        private void UpdateHeightMap(int chunkWorldY)
        {
            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                for (int x = 0; x < ChunkConstants.Size; x++)
                {
                    int columnIndex = z * ChunkConstants.Size + x;
                    int actualSurfaceY = int.MinValue;

                    for (int y = ChunkConstants.Size - 1; y >= 0; y--)
                    {
                        int worldY = chunkWorldY + y;
                        int index = Lithforge.Voxel.Chunk.ChunkData.GetIndex(x, y, z);
                        StateId blockState = ChunkData[index];

                        if (!blockState.Equals(AirId) && !blockState.Equals(WaterId))
                        {
                            actualSurfaceY = worldY;
                            break;
                        }
                    }

                    if (actualSurfaceY != int.MinValue)
                    {
                        HeightMap[columnIndex] = actualSurfaceY;
                    }
                }
            }
        }
    }
}
