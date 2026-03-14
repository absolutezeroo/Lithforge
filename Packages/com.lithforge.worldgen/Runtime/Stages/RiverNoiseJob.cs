using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Climate;
using Lithforge.WorldGen.River;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Stages
{
    /// <summary>
    /// Computes per-column river presence and carve depth using domain-warped noise.
    /// Rivers exist where |warpedNoise| is less than a variable threshold modulated by
    /// erosion (wider in plains, narrower in mountains) and faded near oceans.
    ///
    /// Inputs: ClimateMap (continentalness/erosion gate), HeightMap, seed.
    /// Outputs: RiverFlags (per-column byte, 0 = no river, 1 = river), RiverCarveDepth (float).
    ///
    /// Owner: GenerationPipeline.Schedule allocates both output arrays.
    /// Dispose: RiverFlags transferred to ManagedChunk; RiverCarveDepth transient.
    /// </summary>
    [BurstCompile]
    public struct RiverNoiseJob : IJob
    {
        [ReadOnly] public NativeArray<ClimateData> ClimateMap;
        [ReadOnly] public NativeArray<int> HeightMap;
        [ReadOnly] public NativeRiverConfig Config;
        [ReadOnly] public long Seed;
        [ReadOnly] public int3 ChunkCoord;
        [ReadOnly] public int SeaLevel;

        [WriteOnly] public NativeArray<float> RiverCarveDepth;
        [WriteOnly] public NativeArray<byte> RiverFlags;

        public void Execute()
        {
            int chunkWorldX = ChunkCoord.x * ChunkConstants.Size;
            int chunkWorldZ = ChunkCoord.z * ChunkConstants.Size;

            // Pre-compute seed offsets for the three noise samples
            float seedX = (Seed & 0xFFFF) * 0.3183099f + Config.SeedOffset;
            float seedZ = ((Seed >> 16) & 0xFFFF) * 0.3183099f + Config.SeedOffset;

            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                for (int x = 0; x < ChunkConstants.Size; x++)
                {
                    int columnIndex = z * ChunkConstants.Size + x;
                    ClimateData climate = ClimateMap[columnIndex];
                    int surfaceY = HeightMap[columnIndex];

                    float worldX = chunkWorldX + x;
                    float worldZ = chunkWorldZ + z;

                    // Suppress rivers in ocean zones (low continentalness)
                    float coastFade = math.smoothstep(
                        Config.OceanContinentalnessCutoff - 0.1f,
                        Config.OceanContinentalnessCutoff + 0.1f,
                        climate.Continentalness);

                    // Suppress rivers below sea level (already underwater)
                    float heightFade = math.smoothstep(
                        (float)(SeaLevel - 5),
                        (float)(SeaLevel + 2),
                        (float)surfaceY);

                    // Early out if both fades are negligible
                    float fadeFactor = coastFade * heightFade;
                    if (fadeFactor < 0.01f)
                    {
                        RiverCarveDepth[columnIndex] = 0f;
                        RiverFlags[columnIndex] = 0;
                        continue;
                    }

                    // Domain warp for meandering (2 noise samples)
                    float warpX = Config.WarpStrength * noise.snoise(new float2(
                        (worldX + seedX + 31.7f) * Config.WarpFrequency,
                        (worldZ + seedZ + 47.3f) * Config.WarpFrequency));
                    float warpZ = Config.WarpStrength * noise.snoise(new float2(
                        (worldX + seedX + 83.2f) * Config.WarpFrequency,
                        (worldZ + seedZ + 12.8f) * Config.WarpFrequency));

                    // River noise at warped coordinates (1 noise sample)
                    float riverNoise = noise.snoise(new float2(
                        (worldX + warpX + seedX) * Config.Frequency,
                        (worldZ + warpZ + seedZ) * Config.Frequency));

                    float absNoise = math.abs(riverNoise);

                    // Variable threshold: wider in plains (high erosion), narrower in mountains (low erosion)
                    float erosionFactor = math.remap(0f, 1f, 0.4f, 2.5f, climate.Erosion);
                    float threshold = Config.BaseThreshold * erosionFactor * fadeFactor;

                    // River detection
                    bool isRiver = absNoise < threshold;
                    float intensity = math.select(
                        0f,
                        (1f - absNoise / math.max(threshold, 0.001f)) * fadeFactor,
                        isRiver);

                    // Carve depth: deeper in mountains, shallow in plains
                    float mountainFactor = math.saturate(math.remap(0.3f, 0.7f, 1f, 0f, climate.Erosion));
                    float carveDepth = intensity * math.lerp(
                        Config.MaxCarveDepthPlains,
                        Config.MaxCarveDepthMountain,
                        mountainFactor);

                    RiverCarveDepth[columnIndex] = carveDepth;
                    RiverFlags[columnIndex] = (byte)math.select(0, 1, isRiver);
                }
            }
        }
    }
}
