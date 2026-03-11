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

        // Amplitude for 3D density perturbation that creates overhangs.
        // Blended with the 2D heightmap: density = (surfaceY - worldY) + Sample3D * amplitude.
        // Positive density = solid. Higher values produce more dramatic overhangs.
        private const float _densityAmplitude = 8.0f;

        public void Execute()
        {
            int chunkWorldX = ChunkCoord.x * ChunkConstants.Size;
            int chunkWorldY = ChunkCoord.y * ChunkConstants.Size;
            int chunkWorldZ = ChunkCoord.z * ChunkConstants.Size;

            // Create a separate noise config for 3D density sampling.
            // Use a different seed offset so the 3D field is uncorrelated with the 2D heightmap.
            NativeNoiseConfig densityConfig = NoiseConfig;
            densityConfig.SeedOffset = NoiseConfig.SeedOffset + 1000.0f;

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

                        // Blend 2D heightmap with 3D density field for overhangs
                        float baseDensity = surfaceY - worldY;
                        float noise3D = NativeNoise.Sample3D(worldX, worldY, worldZ, densityConfig, Seed);
                        float density = baseDensity + noise3D * _densityAmplitude;

                        if (density > 0.0f)
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
        }
    }
}
