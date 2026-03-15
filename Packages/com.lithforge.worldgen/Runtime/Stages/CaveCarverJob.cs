using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Noise;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Stages
{
    [BurstCompile]
    public struct CaveCarverJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
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

        public void Execute(int columnIndex)
        {
            int x = columnIndex & (ChunkConstants.Size - 1);
            int z = columnIndex >> 5;

            int chunkWorldX = ChunkCoord.x * ChunkConstants.Size;
            int chunkWorldY = ChunkCoord.y * ChunkConstants.Size;
            int chunkWorldZ = ChunkCoord.z * ChunkConstants.Size;

            NativeNoiseConfig noise1 = CaveNoise;
            noise1.SeedOffset = CaveSeedOffset1;
            NativeNoiseConfig noise2 = CaveNoise;
            noise2.SeedOffset = CaveSeedOffset2;

            float worldX = chunkWorldX + x;
            float worldZ = chunkWorldZ + z;

            for (int y = 0; y < ChunkConstants.Size; y++)
            {
                int worldY = chunkWorldY + y;

                if (worldY < MinCarveY)
                {
                    continue;
                }

                float n1 = NativeNoise.Sample3D(worldX, worldY, worldZ, noise1, Seed);
                float n2 = NativeNoise.Sample3D(worldX, worldY, worldZ, noise2, Seed);

                float caveValue = n1 * n1 + n2 * n2;

                // Modulate threshold by depth: caves are larger deeper underground.
                // At surface (worldY == SeaLevel), factor is 1.0. At Y=0, factor is 1.5.
                float depthFactor = 1.0f + 0.5f * math.saturate((float)(SeaLevel - worldY) / SeaLevel);
                float adjustedThreshold = CaveThreshold * depthFactor;

                if (caveValue < adjustedThreshold)
                {
                    // Don't carve within buffer of sea level to protect ocean floor
                    if (worldY >= SeaLevel - SeaLevelCarveBuffer && worldY <= SeaLevel)
                    {
                        continue;
                    }

                    int index = Lithforge.Voxel.Chunk.ChunkData.GetIndex(x, y, z);
                    StateId current = ChunkData[index];

                    // Don't carve water or air
                    if (current.Equals(WaterId) || current.Equals(AirId))
                    {
                        continue;
                    }

                    ChunkData[index] = AirId;
                }
            }
        }
    }
}
