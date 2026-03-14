using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Biome;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Stages
{
    [BurstCompile]
    public struct SurfaceBuilderJob : IJob
    {
        // ChunkData is aliased across multiple chained jobs via linear JobHandle dependencies.
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<StateId> ChunkData;

        [ReadOnly] public NativeArray<int> HeightMap;
        [ReadOnly] public NativeArray<byte> BiomeMap;
        [ReadOnly] public NativeArray<NativeBiomeData> BiomeData;
        [ReadOnly] public NativeArray<byte> RiverFlags;
        [ReadOnly] public int3 ChunkCoord;
        [ReadOnly] public int SeaLevel;
        [ReadOnly] public long Seed;
        [ReadOnly] public StateId StoneId;
        [ReadOnly] public StateId AirId;
        [ReadOnly] public StateId WaterId;
        [ReadOnly] public StateId IceId;
        [ReadOnly] public StateId GravelId;
        [ReadOnly] public StateId SandId;

        public void Execute()
        {
            int chunkWorldX = ChunkCoord.x * ChunkConstants.Size;
            int chunkWorldY = ChunkCoord.y * ChunkConstants.Size;
            int chunkWorldZ = ChunkCoord.z * ChunkConstants.Size;

            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                for (int x = 0; x < ChunkConstants.Size; x++)
                {
                    int columnIndex = z * ChunkConstants.Size + x;
                    int surfaceY = HeightMap[columnIndex];
                    byte biomeId = BiomeMap[columnIndex];
                    byte riverFlag = RiverFlags[columnIndex];
                    bool isRiver = riverFlag != 0;

                    NativeBiomeData biome = GetBiome(biomeId);
                    StateId topBlock = biome.TopBlock;
                    StateId fillerBlock = biome.FillerBlock;
                    StateId underwaterBlock = biome.UnderwaterBlock;
                    int fillerDepth = biome.FillerDepth;

                    // River bed material override: gravel unless biome surface is sand
                    if (isRiver)
                    {
                        bool isSandBiome = topBlock.Equals(SandId);
                        topBlock = isSandBiome ? SandId : GravelId;
                        underwaterBlock = isSandBiome ? SandId : GravelId;
                    }

                    bool isFrozen = (biome.SurfaceFlags & NativeBiomeSurfaceFlags.IsFrozen) != 0;

                    for (int y = ChunkConstants.Size - 1; y >= 0; y--)
                    {
                        int worldY = chunkWorldY + y;
                        int index = Lithforge.Voxel.Chunk.ChunkData.GetIndex(x, y, z);
                        StateId current = ChunkData[index];

                        // Frozen ocean: replace surface water with ice (patchy)
                        if (isFrozen && current.Equals(WaterId) && worldY == SeaLevel - 1)
                        {
                            uint iceHash = HashColumn(chunkWorldX + x, chunkWorldZ + z, Seed);
                            bool placeIce = (iceHash % 10u) < 8u;
                            if (placeIce)
                            {
                                ChunkData[index] = IceId;
                            }
                            continue;
                        }

                        if (!current.Equals(StoneId))
                        {
                            continue;
                        }

                        int depth = surfaceY - worldY;

                        if (depth >= 0 && depth <= 1)
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

        private static uint HashColumn(int x, int z, long seed)
        {
            uint h = (uint)(seed & 0xFFFFFFFF);
            h ^= (uint)x * 374761393u;
            h ^= (uint)z * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return h;
        }
    }
}
