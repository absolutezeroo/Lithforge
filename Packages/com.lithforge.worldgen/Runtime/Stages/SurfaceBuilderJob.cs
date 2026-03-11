using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Biome;
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
        [ReadOnly] public NativeArray<byte> BiomeMap;
        [ReadOnly] public NativeArray<NativeBiomeData> BiomeData;
        [ReadOnly] public int3 ChunkCoord;
        [ReadOnly] public int SeaLevel;
        [ReadOnly] public StateId StoneId;
        [ReadOnly] public StateId AirId;

        public void Execute()
        {
            int chunkWorldY = ChunkCoord.y * ChunkConstants.Size;

            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                for (int x = 0; x < ChunkConstants.Size; x++)
                {
                    int columnIndex = z * ChunkConstants.Size + x;
                    int surfaceY = HeightMap[columnIndex];
                    byte biomeId = BiomeMap[columnIndex];

                    NativeBiomeData biome = GetBiome(biomeId);
                    StateId topBlock = biome.TopBlock;
                    StateId fillerBlock = biome.FillerBlock;
                    StateId underwaterBlock = biome.UnderwaterBlock;
                    int fillerDepth = biome.FillerDepth;

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
                                ChunkData[index] = topBlock;
                            }
                            else
                            {
                                ChunkData[index] = underwaterBlock;
                            }
                        }
                        else if (depth > 1 && depth <= fillerDepth + 1)
                        {
                            ChunkData[index] = fillerBlock;
                        }
                    }
                }
            }
        }

        private NativeBiomeData GetBiome(byte biomeId)
        {
            // O(1) direct access: BiomeId is a sequential index assigned in Bootstrap.
            // Invariant: BiomeData[i].BiomeId == i, verified at startup.
            return biomeId < BiomeData.Length ? BiomeData[biomeId] : BiomeData[0];
        }
    }
}
