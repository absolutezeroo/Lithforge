using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Biome;
using Lithforge.WorldGen.Climate;
using Lithforge.WorldGen.Noise;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Stages
{
    /// <summary>
    /// Reads per-column ClimateData (from ClimateNoiseJob) and NativeBiomeData to produce:
    ///   - Biome-weighted blended terrain height per column
    ///   - Dominant biome selection per column (hard edge)
    ///   - Filled voxel column (stone/water/air)
    ///
    /// Replaces the old sequential TerrainShapeJob (single noise) + BiomeAssignmentJob (temp/humidity only).
    /// Height blending uses exponential weights in 4D climate space (temp, humidity, continentalness, erosion).
    /// Each biome contributes its BaseHeight + per-biome terrain noise scaled by HeightAmplitude.
    /// </summary>
    [BurstCompile]
    public struct TerrainShapeJob : IJob
    {
        public NativeArray<StateId> ChunkData;
        [WriteOnly] public NativeArray<int> HeightMap;
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

        private const float _weightSharpness = 8.0f;

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
                    ClimateData climate = ClimateMap[columnIndex];

                    float worldX = chunkWorldX + x;
                    float worldZ = chunkWorldZ + z;

                    // Sample base terrain noise once per column (shared across biome blending)
                    float terrainNoise = NativeNoise.Sample2D(
                        worldX, worldZ, TerrainNoise, Seed);

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

                        float weight = math.exp(-_weightSharpness * distSq);
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
                    HeightMap[columnIndex] = surfaceY;
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
                }
            }

            UpdateHeightMap(chunkWorldY);
        }

        /// <summary>
        /// Refines HeightMap by scanning each column top-down for the actual highest
        /// non-air, non-water block. Corrects HeightMap for columns that are entirely
        /// below sea level (filled with water above stone).
        /// </summary>
        private void UpdateHeightMap(int chunkWorldY)
        {
            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                for (int x = 0; x < ChunkConstants.Size; x++)
                {
                    int columnIndex = z * ChunkConstants.Size + x;
                    bool foundAir = false;
                    int actualSurfaceY = int.MinValue;

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

                    if (actualSurfaceY != int.MinValue)
                    {
                        HeightMap[columnIndex] = actualSurfaceY;
                    }
                }
            }
        }
    }
}
