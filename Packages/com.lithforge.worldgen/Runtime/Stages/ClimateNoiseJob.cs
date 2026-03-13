using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Climate;
using Lithforge.WorldGen.Noise;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Stages
{
    /// <summary>
    /// Samples 4 climate noise parameters (temperature, humidity, continentalness, erosion)
    /// per XZ column and writes normalized [0,1] values into a ClimateData array.
    ///
    /// Temperature and humidity use noise.snoise (backward-compatible).
    /// Continentalness and erosion use noise.cnoise (24-42% faster).
    ///
    /// Owner: GenerationPipeline.Schedule allocates ClimateMap (Persistent).
    /// Dispose: GenerationHandle.Dispose after downstream jobs complete.
    /// </summary>
    [BurstCompile]
    public struct ClimateNoiseJob : IJob
    {
        [WriteOnly] public NativeArray<ClimateData> ClimateMap;

        [ReadOnly] public long Seed;
        [ReadOnly] public int3 ChunkCoord;
        [ReadOnly] public NativeNoiseConfig TemperatureNoise;
        [ReadOnly] public NativeNoiseConfig HumidityNoise;
        [ReadOnly] public NativeNoiseConfig ContinentalnessNoise;
        [ReadOnly] public NativeNoiseConfig ErosionNoise;

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

                    float temperature = NativeNoise.Sample2D(
                        worldX, worldZ, TemperatureNoise, Seed) * 0.5f + 0.5f;
                    float humidity = NativeNoise.Sample2D(
                        worldX, worldZ, HumidityNoise, Seed) * 0.5f + 0.5f;
                    float continentalness = NativeNoise.Sample2DCnoise(
                        worldX, worldZ, ContinentalnessNoise, Seed) * 0.5f + 0.5f;
                    float erosion = NativeNoise.Sample2DCnoise(
                        worldX, worldZ, ErosionNoise, Seed) * 0.5f + 0.5f;

                    int columnIndex = z * ChunkConstants.Size + x;

                    ClimateMap[columnIndex] = new ClimateData
                    {
                        Temperature = math.clamp(temperature, 0.0f, 1.0f),
                        Humidity = math.clamp(humidity, 0.0f, 1.0f),
                        Continentalness = math.clamp(continentalness, 0.0f, 1.0f),
                        Erosion = math.clamp(erosion, 0.0f, 1.0f),
                    };
                }
            }
        }
    }
}