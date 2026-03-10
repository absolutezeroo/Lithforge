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
    public struct CaveCarverJob : IJob
    {
        public NativeArray<StateId> ChunkData;

        [ReadOnly] public long Seed;
        [ReadOnly] public int3 ChunkCoord;
        [ReadOnly] public NativeNoiseConfig CaveNoise;
        [ReadOnly] public StateId AirId;
        [ReadOnly] public StateId WaterId;
        [ReadOnly] public int SeaLevel;
        [ReadOnly] public float CaveThreshold;
        [ReadOnly] public int MinCarveY;
        [ReadOnly] public int CaveSeedOffset1;
        [ReadOnly] public int CaveSeedOffset2;
        [ReadOnly] public int SeaLevelCarveBuffer;

        public void Execute()
        {
            int chunkWorldX = ChunkCoord.x * ChunkConstants.Size;
            int chunkWorldY = ChunkCoord.y * ChunkConstants.Size;
            int chunkWorldZ = ChunkCoord.z * ChunkConstants.Size;

            NativeNoiseConfig noise1 = CaveNoise;
            noise1.SeedOffset = CaveSeedOffset1;

            NativeNoiseConfig noise2 = CaveNoise;
            noise2.SeedOffset = CaveSeedOffset2;

            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                for (int y = 0; y < ChunkConstants.Size; y++)
                {
                    int worldY = chunkWorldY + y;

                    if (worldY < MinCarveY)
                    {
                        continue;
                    }

                    for (int x = 0; x < ChunkConstants.Size; x++)
                    {
                        float worldX = chunkWorldX + x;
                        float worldZ = chunkWorldZ + z;

                        float n1 = NativeNoise.Sample3D(worldX, worldY, worldZ, noise1, Seed);
                        float n2 = NativeNoise.Sample3D(worldX, worldY, worldZ, noise2, Seed);

                        float caveValue = n1 * n1 + n2 * n2;

                        if (caveValue < CaveThreshold)
                        {
                            // Don't carve within buffer of sea level to protect ocean floor
                            if (worldY >= SeaLevel - SeaLevelCarveBuffer && worldY <= SeaLevel)
                            {
                                continue;
                            }

                            int index = Lithforge.Voxel.Chunk.ChunkData.GetIndex(x, y, z);
                            StateId current = ChunkData[index];

                            // Don't carve water blocks
                            if (current.Equals(WaterId))
                            {
                                continue;
                            }

                            // Don't carve air
                            if (current.Equals(AirId))
                            {
                                continue;
                            }

                            ChunkData[index] = AirId;
                        }
                    }
                }
            }
        }
    }
}
