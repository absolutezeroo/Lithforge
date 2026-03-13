using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Biome;
using Lithforge.WorldGen.Climate;
using Lithforge.WorldGen.Noise;
using Lithforge.WorldGen.Ore;
using Lithforge.WorldGen.Stages;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Pipeline
{
    public sealed class GenerationPipeline
    {
        private readonly NativeNoiseConfig _terrainNoise;
        private readonly NativeNoiseConfig _temperatureNoise;
        private readonly NativeNoiseConfig _humidityNoise;
        private readonly NativeNoiseConfig _continentalnessNoise;
        private readonly NativeNoiseConfig _erosionNoise;
        private readonly NativeNoiseConfig _caveNoise;
        private readonly float _caveThreshold;
        private readonly int _minCarveY;
        private readonly int _caveSeedOffset1;
        private readonly int _caveSeedOffset2;
        private readonly int _seaLevelCarveBuffer;
        private readonly NativeArray<NativeBiomeData> _biomeData;
        private readonly NativeArray<NativeOreConfig> _oreConfigs;
        private readonly NativeArray<BlockStateCompact> _stateTable;
        private readonly StateId _stoneId;
        private readonly StateId _airId;
        private readonly StateId _waterId;
        private readonly int _seaLevel;

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
            int seaLevel)
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
            _seaLevel = seaLevel;
        }

        public GenerationHandle Schedule(int3 coord, long seed, NativeArray<StateId> chunkData)
        {
            NativeArray<int> heightMap = new NativeArray<int>(
                ChunkConstants.SizeSquared, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            NativeArray<byte> lightData = new NativeArray<byte>(
                ChunkConstants.Volume, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            NativeArray<byte> biomeMap = new NativeArray<byte>(
                ChunkConstants.SizeSquared, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            NativeArray<ClimateData> climateMap = new NativeArray<ClimateData>(
                ChunkConstants.SizeSquared, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // Stage 1: Climate noise sampling (4 parameters per column)
            ClimateNoiseJob climateJob = new ClimateNoiseJob
            {
                ClimateMap = climateMap,
                Seed = seed,
                ChunkCoord = coord,
                TemperatureNoise = _temperatureNoise,
                HumidityNoise = _humidityNoise,
                ContinentalnessNoise = _continentalnessNoise,
                ErosionNoise = _erosionNoise,
            };

            JobHandle climateHandle = climateJob.Schedule();

            // Stage 2: Terrain shape with biome-weighted height blending
            TerrainShapeJob terrainJob = new TerrainShapeJob
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

            JobHandle terrainHandle = terrainJob.Schedule(climateHandle);

            // Stage 3: Cave carving
            CaveCarverJob caveJob = new CaveCarverJob
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

            JobHandle caveHandle = caveJob.Schedule(terrainHandle);

            // Stage 4: Surface builder (biome-driven)
            SurfaceBuilderJob surfaceJob = new SurfaceBuilderJob
            {
                ChunkData = chunkData,
                HeightMap = heightMap,
                BiomeMap = biomeMap,
                BiomeData = _biomeData,
                ChunkCoord = coord,
                SeaLevel = _seaLevel,
                StoneId = _stoneId,
                AirId = _airId,
            };

            JobHandle surfaceHandle = surfaceJob.Schedule(caveHandle);

            // Stage 5: Ore generation
            OreGenerationJob oreJob = new OreGenerationJob
            {
                ChunkData = chunkData,
                OreConfigs = _oreConfigs,
                Seed = seed,
                ChunkCoord = coord,
                StoneId = _stoneId,
            };

            JobHandle oreHandle = oreJob.Schedule(surfaceHandle);

            // Stage 6: Initial lighting
            InitialLightingJob lightingJob = new InitialLightingJob
            {
                HeightMap = heightMap,
                ChunkCoord = coord,
                ChunkData = chunkData,
                StateTable = _stateTable,
                LightData = lightData,
            };

            JobHandle lightingHandle = lightingJob.Schedule(oreHandle);

            // Stage 7: Light propagation (BFS flood fill)
            // Border light output for cross-chunk propagation.
            // Owner: GenerationHandle. Dispose: GenerationScheduler after reading.
            NativeList<NativeBorderLightEntry> borderLightOutput =
                new NativeList<NativeBorderLightEntry>(256, Allocator.Persistent);

            LightPropagationJob lightPropJob = new LightPropagationJob
            {
                ChunkData = chunkData,
                StateTable = _stateTable,
                LightData = lightData,
                BorderLightOutput = borderLightOutput,
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
            };
        }
    }
}
