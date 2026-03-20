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
    ///     Computes per-column river presence and carve depth using domain-warped noise.
    ///     Rivers exist where |warpedNoise| is less than a variable threshold modulated by
    ///     erosion (wider in plains, narrower in mountains) and faded near oceans.
    ///     Inputs: ClimateMap (continentalness/erosion gate), HeightMap, seed.
    ///     Outputs: RiverFlags (per-column byte, 0 = no river, 1 = river), RiverCarveDepth (float).
    ///     Owner: GenerationPipeline.Schedule allocates both output arrays.
    ///     Dispose: RiverFlags transferred to ManagedChunk; RiverCarveDepth transient.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic)]
    public struct RiverNoiseJob : IJobParallelFor
    {
        /// <summary>Per-column climate data for continentalness and erosion gating.</summary>
        [ReadOnly] public NativeArray<ClimateData> ClimateMap;

        /// <summary>Per-column surface height from TerrainShapeJob.</summary>
        [ReadOnly] public NativeArray<int> HeightMap;

        /// <summary>River noise and carving configuration parameters.</summary>
        [ReadOnly] public NativeRiverConfig Config;

        /// <summary>World seed for deterministic river noise generation.</summary>
        [ReadOnly] public long Seed;

        /// <summary>Chunk coordinate in chunk-space.</summary>
        [ReadOnly] public int3 ChunkCoord;

        /// <summary>World-space sea level for underwater suppression.</summary>
        [ReadOnly] public int SeaLevel;

        /// <summary>Output per-column carve depth in blocks, consumed by RiverCarveJob.</summary>
        [WriteOnly] public NativeArray<float> RiverCarveDepth;

        /// <summary>Output per-column river flag (0 = no river, 1 = river).</summary>
        [WriteOnly] public NativeArray<byte> RiverFlags;

        /// <summary>Evaluates river presence and carve depth for a single XZ column.</summary>
        public void Execute(int columnIndex)
        {
            int x = columnIndex & ChunkConstants.Size - 1;
            int z = columnIndex >> 5;

            ClimateData climate = ClimateMap[columnIndex];
            int surfaceY = HeightMap[columnIndex];

            float worldX = ChunkCoord.x * ChunkConstants.Size + x;
            float worldZ = ChunkCoord.z * ChunkConstants.Size + z;

            float seedX = (Seed & 0xFFFF) * 0.3183099f + Config.SeedOffset;
            float seedZ = (Seed >> 16 & 0xFFFF) * 0.3183099f + Config.SeedOffset;

            // Suppress rivers in ocean zones (low continentalness)
            float coastFade = math.smoothstep(
                Config.OceanContinentalnessCutoff - 0.1f,
                Config.OceanContinentalnessCutoff + 0.1f,
                climate.Continentalness);

            // Suppress rivers below sea level (already underwater)
            float heightFade = math.smoothstep(
                SeaLevel - 1,
                SeaLevel + 4,
                surfaceY);

            // Early out if both fades are negligible
            float fadeFactor = coastFade * heightFade;
            if (fadeFactor < 0.01f)
            {
                RiverCarveDepth[columnIndex] = 0f;
                RiverFlags[columnIndex] = 0;
                return;
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
            float threshold = Config.BaseThreshold * erosionFactor * coastFade;

            // River detection
            bool isRiver = absNoise < threshold;
            float intensity = math.select(
                0f,
                (1f - absNoise / math.max(threshold, 0.001f)) * coastFade * heightFade,
                isRiver);

            // Carve depth: deeper in mountains, shallow in plains
            float mountainFactor = math.saturate(math.remap(0.3f, 0.7f, 1f, 0f, climate.Erosion));
            float carveDepth = intensity * math.lerp(
                Config.MaxCarveDepthPlains,
                Config.MaxCarveDepthMountain,
                mountainFactor);

            // Suppress river if carved channel cannot reach near sea level.
            // A river with no water (channel entirely above seaLevel) serves no purpose
            // and would require liquid sim to fill it — which would spread water incorrectly.
            int projectedBedY = surfaceY - (int)carveDepth;
            if (projectedBedY > SeaLevel + 2)
            {
                RiverCarveDepth[columnIndex] = 0f;
                RiverFlags[columnIndex] = 0;
                return;
            }

            // Bank smoothing: columns outside the river threshold but within the bank
            // zone get a partial carveDepth that slopes terrain down toward sea level.
            float bankThreshold = threshold * 3f;
            bool isBank = !isRiver && absNoise < bankThreshold && fadeFactor > 0.01f;

            if (isBank)
            {
                // bankT: 1.0 at river edge (absNoise=threshold), 0.0 at bank edge (absNoise=bankThreshold)
                float bankT = 1f - (absNoise - threshold) / (bankThreshold - threshold);
                bankT = bankT * bankT; // quadratic falloff: steep near river, gentle far

                // Only lower terrain above sea level — don't carve underwater banks
                float aboveWater = math.max(0f, surfaceY - (float)SeaLevel);
                float bankCarve = bankT * aboveWater * 0.75f * coastFade;

                RiverCarveDepth[columnIndex] = bankCarve;
                RiverFlags[columnIndex] = 0; // bank columns don't get gravel
            }
            else
            {
                RiverCarveDepth[columnIndex] = carveDepth;
                RiverFlags[columnIndex] = (byte)math.select(0, 1, isRiver);
            }
        }
    }
}
