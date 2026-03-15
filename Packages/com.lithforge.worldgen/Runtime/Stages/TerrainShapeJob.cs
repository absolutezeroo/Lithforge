using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Biome;
using Lithforge.WorldGen.Climate;
using Lithforge.WorldGen.Noise;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Stages
{
    /// <summary>
    /// Parallel-per-column terrain generation. Each column (x,z) is independent:
    ///   - Biome-weighted blended terrain height
    ///   - Dominant biome selection (hard edge)
    ///   - Filled voxel column (stone/water/air)
    ///   - Refined HeightMap (fused top-down scan)
    ///
    /// Schedule with columnCount=ChunkConstants.SizeSquared (1024), batchSize=32
    /// (one Z row for Y-major cache locality).
    /// </summary>
    [BurstCompile]
    public struct TerrainShapeJob : IJobParallelFor
    {
        // Each column writes to distinct Y-major indices (y*1024 + z*32 + x).
        // No overlap between columns. NativeDisableParallelForRestriction needed
        // because write indices don't match the parallel-for index.
        [NativeDisableParallelForRestriction]
        public NativeArray<StateId> ChunkData;

        [NativeDisableParallelForRestriction]
        [WriteOnly] public NativeArray<int> HeightMap;

        [NativeDisableParallelForRestriction]
        [WriteOnly] public NativeArray<byte> BiomeMap;

        [ReadOnly] public NativeArray<ClimateData> ClimateMap;
        [ReadOnly] public NativeArray<NativeBiomeData> BiomeData;
        [ReadOnly] public long Seed;
        [ReadOnly] public int3 ChunkCoord;
        [ReadOnly] public NativeNoiseConfig TerrainNoise;
        [ReadOnly] public int SeaLevel;
        [ReadOnly] public StateId StoneId;
        [ReadOnly] public StateId WaterId;
        [ReadOnly] public StateId AirId;

        public void Execute(int columnIndex)
        {
            int x = columnIndex & (ChunkConstants.Size - 1);
            int z = columnIndex >> 5;

            int chunkWorldX = ChunkCoord.x * ChunkConstants.Size;
            int chunkWorldY = ChunkCoord.y * ChunkConstants.Size;
            int chunkWorldZ = ChunkCoord.z * ChunkConstants.Size;

            ClimateData climate = ClimateMap[columnIndex];
            float worldX = chunkWorldX + x;
            float worldZ = chunkWorldZ + z;

            // Sample base terrain noise once per column (shared across biome blending)
            float terrainNoise = NativeNoise.Sample2D(worldX, worldZ, TerrainNoise, Seed);

            // Compute per-biome exponential weights in 4D climate space
            float totalWeight = 0.0f;
            float blendedHeight = 0.0f;
            byte dominantBiome = 0;
            float dominantWeight = -1.0f;

            for (int i = 0; i < BiomeData.Length; i++)
            {
                NativeBiomeData biome = BiomeData[i];

                float dTemp = climate.Temperature - biome.TemperatureCenter;
                float dHum = climate.Humidity - biome.HumidityCenter;
                float dCont = climate.Continentalness - biome.ContinentalnessCenter;
                float dEro = climate.Erosion - biome.ErosionCenter;
                float distSq = dTemp * dTemp + dHum * dHum
                             + dCont * dCont + dEro * dEro;

                float weight = math.exp(-biome.WeightSharpness * distSq);
                totalWeight += weight;

                // Each biome contributes its base height + scaled terrain noise
                float biomeHeight = biome.BaseHeight + terrainNoise * biome.HeightAmplitude;
                blendedHeight += weight * biomeHeight;

                // Track dominant biome (branchless via math.select)
                bool isBetter = weight > dominantWeight;
                dominantBiome = (byte)math.select((int)dominantBiome, (int)biome.BiomeId, isBetter);
                dominantWeight = math.select(dominantWeight, weight, isBetter);
            }

            // Normalize blended height
            if (totalWeight > 0.0f)
            {
                blendedHeight /= totalWeight;
            }

            int surfaceY = SeaLevel + (int)math.round(blendedHeight);
            BiomeMap[columnIndex] = dominantBiome;

            // Fill voxel column
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

            // Fused HeightMap refinement: scan top-down for actual highest solid block.
            // Previously a separate UpdateHeightMap pass — merged here to avoid a
            // second job or synchronization barrier.
            bool foundAir = false;
            int actualSurfaceY = surfaceY;

            for (int y = ChunkConstants.Size - 1; y >= 0; y--)
            {
                int index = Lithforge.Voxel.Chunk.ChunkData.GetIndex(x, y, z);
                StateId blockState = ChunkData[index];

                if (blockState.Equals(AirId) || blockState.Equals(WaterId))
                {
                    foundAir = true;
                }
                else if (foundAir)
                {
                    actualSurfaceY = chunkWorldY + y;
                    break;
                }
            }

            HeightMap[columnIndex] = actualSurfaceY;
        }
    }
}
