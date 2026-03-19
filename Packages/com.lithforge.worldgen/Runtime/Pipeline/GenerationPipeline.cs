using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Biome;
using Lithforge.WorldGen.Climate;
using Lithforge.WorldGen.Lighting;
using Lithforge.WorldGen.Noise;
using Lithforge.WorldGen.Ore;
using Lithforge.WorldGen.River;
using Lithforge.WorldGen.Stages;

using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Pipeline
{
    /// <summary>
    /// Schedules the complete 9-stage Burst generation pipeline for a single chunk.
    /// Stages: Climate → Terrain → RiverNoise → RiverCarve → Cave → Surface → Ore → InitialLighting → LightPropagation.
    /// </summary>
    public sealed class GenerationPipeline
    {
        /// <summary>Air block state for empty voxels.</summary>
        private readonly StateId _airId;

        /// <summary>Per-biome native data for terrain shaping and surface building.</summary>
        private readonly NativeArray<NativeBiomeData> _biomeData;

        /// <summary>Noise config for spaghetti cave generation.</summary>
        private readonly NativeNoiseConfig _caveNoise;

        /// <summary>First seed offset for cave noise decorrelation.</summary>
        private readonly int _caveSeedOffset1;

        /// <summary>Second seed offset for cave noise decorrelation.</summary>
        private readonly int _caveSeedOffset2;

        /// <summary>Threshold below which dual-noise cave carving activates.</summary>
        private readonly float _caveThreshold;

        /// <summary>Noise config for the continentalness climate layer.</summary>
        private readonly NativeNoiseConfig _continentalnessNoise;

        /// <summary>Noise config for the erosion climate layer.</summary>
        private readonly NativeNoiseConfig _erosionNoise;

        /// <summary>Gravel state for river/ocean bed placement.</summary>
        private readonly StateId _gravelId;

        /// <summary>Noise config for the humidity climate layer.</summary>
        private readonly NativeNoiseConfig _humidityNoise;

        /// <summary>Ice state for frozen ocean/river surfaces.</summary>
        private readonly StateId _iceId;

        /// <summary>Minimum Y coordinate where cave carving is allowed.</summary>
        private readonly int _minCarveY;

        /// <summary>Per-ore-type generation configs for the ore placement stage.</summary>
        private readonly NativeArray<NativeOreConfig> _oreConfigs;

        /// <summary>River noise and carving parameters.</summary>
        private readonly NativeRiverConfig _riverConfig;

        /// <summary>Sand state for beach and river bed placement.</summary>
        private readonly StateId _sandId;

        /// <summary>World-space sea level used by terrain, surface, and river stages.</summary>
        private readonly int _seaLevel;

        /// <summary>Buffer blocks above sea level where cave carving is suppressed.</summary>
        private readonly int _seaLevelCarveBuffer;

        /// <summary>Block state compact table for light opacity lookups.</summary>
        private readonly NativeArray<BlockStateCompact> _stateTable;

        /// <summary>Stone state used as the base terrain fill and ore replacement target.</summary>
        private readonly StateId _stoneId;

        /// <summary>Noise config for the temperature climate layer.</summary>
        private readonly NativeNoiseConfig _temperatureNoise;

        /// <summary>Noise config for base terrain height generation.</summary>
        private readonly NativeNoiseConfig _terrainNoise;

        /// <summary>Water state for ocean and river fill.</summary>
        private readonly StateId _waterId;

        /// <summary>Creates a pipeline with all noise configs, biome data, ore configs, and block state IDs.</summary>
        public GenerationPipeline(
            NativeNoiseConfig terrainNoise,
            NativeNoiseConfig temperatureNoise,
            NativeNoiseConfig humidityNoise,
            NativeNoiseConfig continentalnessNoise,
            NativeNoiseConfig erosionNoise,
            NativeNoiseConfig caveNoise,
            float caveThreshold,
            int minCarveY,
            int caveSeedOffset1,
            int caveSeedOffset2,
            int seaLevelCarveBuffer,
            NativeArray<NativeBiomeData> biomeData,
            NativeArray<NativeOreConfig> oreConfigs,
            NativeArray<BlockStateCompact> stateTable,
            StateId stoneId,
            StateId airId,
            StateId waterId,
            StateId iceId,
            StateId gravelId,
            StateId sandId,
            int seaLevel,
            NativeRiverConfig riverConfig)
        {
            _terrainNoise = terrainNoise;
            _temperatureNoise = temperatureNoise;
            _humidityNoise = humidityNoise;
            _continentalnessNoise = continentalnessNoise;
            _erosionNoise = erosionNoise;
            _caveNoise = caveNoise;
            _caveThreshold = caveThreshold;
            _minCarveY = minCarveY;
            _caveSeedOffset1 = caveSeedOffset1;
            _caveSeedOffset2 = caveSeedOffset2;
            _seaLevelCarveBuffer = seaLevelCarveBuffer;
            _biomeData = biomeData;
            _oreConfigs = oreConfigs;
            _stateTable = stateTable;
            _stoneId = stoneId;
            _airId = airId;
            _waterId = waterId;
            _iceId = iceId;
            _gravelId = gravelId;
            _sandId = sandId;
            _seaLevel = seaLevel;
            _riverConfig = riverConfig;
        }

        /// <summary>Allocates output arrays and schedules all 9 generation stages as a dependency chain.</summary>
        public GenerationHandle Schedule(int3 coord, long seed, NativeArray<StateId> chunkData)
        {
            NativeArray<int> heightMap = new(
                ChunkConstants.SizeSquared, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            NativeArray<byte> lightData = new(
                ChunkConstants.Volume, Allocator.Persistent);

            NativeArray<byte> biomeMap = new(
                ChunkConstants.SizeSquared, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            NativeArray<ClimateData> climateMap = new(
                ChunkConstants.SizeSquared, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // River arrays: flags are Persistent (transferred to ManagedChunk),
            // carve depth is Persistent (transient, consumed by RiverCarveJob only,
            // disposed in GenerationHandle.Dispose).
            NativeArray<byte> riverFlags = new(
                ChunkConstants.SizeSquared, Allocator.Persistent);

            NativeArray<float> riverCarveDepth = new(
                ChunkConstants.SizeSquared, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // Stage 1: Climate noise sampling (4 parameters per column)
            ClimateNoiseJob climateJob = new()
            {
                ClimateMap = climateMap,
                Seed = seed,
                ChunkCoord = coord,
                TemperatureNoise = _temperatureNoise,
                HumidityNoise = _humidityNoise,
                ContinentalnessNoise = _continentalnessNoise,
                ErosionNoise = _erosionNoise,
            };

            JobHandle climateHandle = climateJob.Schedule(ChunkConstants.SizeSquared, 64);

            // Stage 2: Terrain shape with biome-weighted height blending
            TerrainShapeJob terrainJob = new()
            {
                ChunkData = chunkData,
                HeightMap = heightMap,
                BiomeMap = biomeMap,
                ClimateMap = climateMap,
                BiomeData = _biomeData,
                Seed = seed,
                ChunkCoord = coord,
                TerrainNoise = _terrainNoise,
                SeaLevel = _seaLevel,
                StoneId = _stoneId,
                WaterId = _waterId,
                AirId = _airId,
            };

            JobHandle terrainHandle = terrainJob.Schedule(ChunkConstants.SizeSquared, 32, climateHandle);

            // Stage 3: River noise (2D per-column, domain-warped threshold band)
            RiverNoiseJob riverNoiseJob = new()
            {
                ClimateMap = climateMap,
                HeightMap = heightMap,
                Config = _riverConfig,
                Seed = seed,
                ChunkCoord = coord,
                SeaLevel = _seaLevel,
                RiverCarveDepth = riverCarveDepth,
                RiverFlags = riverFlags,
            };

            JobHandle riverNoiseHandle = riverNoiseJob.Schedule(ChunkConstants.SizeSquared, 64, terrainHandle);

            // Stage 4: River carving (3D, applies carve depth to voxel array)
            RiverCarveJob riverCarveJob = new()
            {
                ChunkData = chunkData,
                RiverCarveDepth = riverCarveDepth,
                HeightMap = heightMap,
                ChunkCoord = coord,
                AirId = _airId,
                WaterId = _waterId,
                SeaLevel = _seaLevel,
            };

            JobHandle riverCarveHandle = riverCarveJob.Schedule(ChunkConstants.SizeSquared, 64, riverNoiseHandle);

            // Stage 5: Cave carving
            CaveCarverJob caveJob = new()
            {
                ChunkData = chunkData,
                Seed = seed,
                ChunkCoord = coord,
                CaveNoise = _caveNoise,
                AirId = _airId,
                WaterId = _waterId,
                SeaLevel = _seaLevel,
                CaveThreshold = _caveThreshold,
                MinCarveY = _minCarveY,
                CaveSeedOffset1 = _caveSeedOffset1,
                CaveSeedOffset2 = _caveSeedOffset2,
                SeaLevelCarveBuffer = _seaLevelCarveBuffer,
            };

            JobHandle caveHandle = caveJob.Schedule(ChunkConstants.SizeSquared, 32, riverCarveHandle);

            // Stage 6: Surface builder (biome-driven, reads river flags)
            SurfaceBuilderJob surfaceJob = new()
            {
                ChunkData = chunkData,
                HeightMap = heightMap,
                BiomeMap = biomeMap,
                BiomeData = _biomeData,
                RiverFlags = riverFlags,
                ChunkCoord = coord,
                SeaLevel = _seaLevel,
                Seed = seed,
                StoneId = _stoneId,
                AirId = _airId,
                WaterId = _waterId,
                IceId = _iceId,
                GravelId = _gravelId,
                SandId = _sandId,
            };

            JobHandle surfaceHandle = surfaceJob.Schedule(ChunkConstants.SizeSquared, 32, caveHandle);

            // Stage 7: Ore generation
            OreGenerationJob oreJob = new()
            {
                ChunkData = chunkData,
                OreConfigs = _oreConfigs,
                Seed = seed,
                ChunkCoord = coord,
                StoneId = _stoneId,
            };

            JobHandle oreHandle = oreJob.Schedule(surfaceHandle);

            // Stage 8: Initial lighting
            InitialLightingJob lightingJob = new()
            {
                HeightMap = heightMap,
                ChunkCoord = coord,
                ChunkData = chunkData,
                StateTable = _stateTable,
                LightData = lightData,
            };

            JobHandle lightingHandle = lightingJob.Schedule(oreHandle);

            // Stage 9: Light propagation (BFS flood fill)
            // Border light output for cross-chunk propagation.
            // Owner: GenerationHandle. Dispose: GenerationScheduler after reading.
            NativeList<NativeBorderLightEntry> borderLightOutput = new(256, Allocator.Persistent);

            LightPropagationJob lightPropJob = new()
            {
                ChunkData = chunkData, StateTable = _stateTable, LightData = lightData, BorderLightOutput = borderLightOutput,
            };

            JobHandle lightPropHandle = lightPropJob.Schedule(lightingHandle);

            return new GenerationHandle
            {
                FinalHandle = lightPropHandle,
                HeightMap = heightMap,
                LightData = lightData,
                BiomeMap = biomeMap,
                ClimateMap = climateMap,
                BorderLightOutput = borderLightOutput,
                RiverFlags = riverFlags,
                RiverCarveDepth = riverCarveDepth,
            };
        }
    }
}
