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

        private const float _caveThreshold = 0.03f;
        private const int _minCarveY = 5;

        public void Execute()
        {
            int chunkWorldX = ChunkCoord.x * ChunkConstants.Size;
            int chunkWorldY = ChunkCoord.y * ChunkConstants.Size;
            int chunkWorldZ = ChunkCoord.z * ChunkConstants.Size;

            NativeNoiseConfig noise1 = CaveNoise;
            noise1.SeedOffset = 0;

            NativeNoiseConfig noise2 = CaveNoise;
            noise2.SeedOffset = 31337;

            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                for (int y = 0; y < ChunkConstants.Size; y++)
                {
                    int worldY = chunkWorldY + y;

                    if (worldY < _minCarveY)
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

                        if (caveValue < _caveThreshold)
                        {
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
