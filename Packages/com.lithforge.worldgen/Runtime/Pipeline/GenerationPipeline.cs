using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Noise;
using Lithforge.WorldGen.Stages;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.WorldGen.Pipeline
{
    public sealed class GenerationPipeline
    {
        private readonly NativeNoiseConfig _terrainNoise;
        private readonly NativeStateRegistry _stateRegistry;
        private readonly StateId _stoneId;
        private readonly StateId _airId;
        private readonly StateId _waterId;
        private readonly StateId _grassId;
        private readonly StateId _dirtId;
        private readonly int _seaLevel;

        public GenerationPipeline(
            NativeNoiseConfig terrainNoise,
            NativeStateRegistry stateRegistry,
            StateId stoneId,
            StateId airId,
            StateId waterId,
            StateId grassId,
            StateId dirtId,
            int seaLevel)
        {
            _terrainNoise = terrainNoise;
            _stateRegistry = stateRegistry;
            _stoneId = stoneId;
            _airId = airId;
            _waterId = waterId;
            _grassId = grassId;
            _dirtId = dirtId;
            _seaLevel = seaLevel;
        }

        public GenerationHandle Schedule(int3 coord, long seed, NativeArray<StateId> chunkData)
        {
            NativeArray<int> heightMap = new NativeArray<int>(
                ChunkConstants.SizeSquared, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            NativeArray<byte> lightData = new NativeArray<byte>(
                ChunkConstants.Volume, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            TerrainShapeJob terrainJob = new TerrainShapeJob
            {
                ChunkData = chunkData,
                HeightMap = heightMap,
                Seed = seed,
                ChunkCoord = coord,
                NoiseConfig = _terrainNoise,
                SeaLevel = _seaLevel,
                StoneId = _stoneId,
                WaterId = _waterId,
                AirId = _airId,
            };

            JobHandle terrainHandle = terrainJob.Schedule();

            SurfaceBuilderJob surfaceJob = new SurfaceBuilderJob
            {
                ChunkData = chunkData,
                HeightMap = heightMap,
                ChunkCoord = coord,
                SeaLevel = _seaLevel,
                GrassId = _grassId,
                DirtId = _dirtId,
                StoneId = _stoneId,
                AirId = _airId,
            };

            JobHandle surfaceHandle = surfaceJob.Schedule(terrainHandle);

            InitialLightingJob lightingJob = new InitialLightingJob
            {
                ChunkData = chunkData,
                HeightMap = heightMap,
                StateTable = _stateRegistry.States,
                ChunkCoord = coord,
                LightData = lightData,
            };

            JobHandle lightingHandle = lightingJob.Schedule(surfaceHandle);

            return new GenerationHandle
            {
                FinalHandle = lightingHandle,
                HeightMap = heightMap,
                LightData = lightData,
            };
        }
    }
}
