using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Biome;
using Lithforge.WorldGen.Noise;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Stages
{
    [BurstCompile]
    public struct BiomeAssignmentJob : IJob
    {
        [ReadOnly] public NativeArray<int> HeightMap;
        [ReadOnly] public NativeArray<NativeBiomeData> BiomeData;
        [ReadOnly] public long Seed;
        [ReadOnly] public int3 ChunkCoord;
        [ReadOnly] public NativeNoiseConfig TemperatureNoise;
        [ReadOnly] public NativeNoiseConfig HumidityNoise;

        public NativeArray<byte> BiomeMap;
        public NativeArray<float> TemperatureMap;
        public NativeArray<float> HumidityMap;

        public void Execute()
        {
            int chunkWorldX = ChunkCoord.x * ChunkConstants.Size;
            int chunkWorldZ = ChunkCoord.z * ChunkConstants.Size;

            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                for (int x = 0; x < ChunkConstants.Size; x++)
                {
                    float worldX = chunkWorldX + x;
                    float worldZ = chunkWorldZ + z;

                    float temperature = NativeNoise.Sample2D(worldX, worldZ, TemperatureNoise, Seed) * 0.5f + 0.5f;
                    float humidity = NativeNoise.Sample2D(worldX, worldZ, HumidityNoise, Seed) * 0.5f + 0.5f;

                    temperature = math.clamp(temperature, 0.0f, 1.0f);
                    humidity = math.clamp(humidity, 0.0f, 1.0f);

                    int columnIndex = z * ChunkConstants.Size + x;
                    TemperatureMap[columnIndex] = temperature;
                    HumidityMap[columnIndex] = humidity;

                    byte bestBiome = 0;
                    float bestDistance = float.MaxValue;

                    for (int i = 0; i < BiomeData.Length; i++)
                    {
                        NativeBiomeData biome = BiomeData[i];

                        if (temperature < biome.TemperatureMin || temperature > biome.TemperatureMax)
                        {
                            continue;
                        }

                        if (humidity < biome.HumidityMin || humidity > biome.HumidityMax)
                        {
                            continue;
                        }

                        float tempDist = temperature - biome.TemperatureCenter;
                        float humDist = humidity - biome.HumidityCenter;
                        float dist = tempDist * tempDist + humDist * humDist;

                        if (dist < bestDistance)
                        {
                            bestDistance = dist;
                            bestBiome = biome.BiomeId;
                        }
                    }

                    BiomeMap[columnIndex] = bestBiome;
                }
            }
        }
    }
}
