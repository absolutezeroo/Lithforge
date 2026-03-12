using System.Collections.Generic;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.Spawn;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;
using Lithforge.WorldGen.Decoration;
using Lithforge.WorldGen.Pipeline;
using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime
{
    public sealed class GameLoop : MonoBehaviour
    {
        private ChunkManager _chunkManager;
        private ChunkMeshStore _chunkMeshStore;
        private GenerationScheduler _generationScheduler;
        private MeshScheduler _meshScheduler;
        private LODScheduler _lodScheduler;
        private ChunkCulling _culling;
        private SpawnManager _spawnManager;
        private Camera _mainCamera;
        private WorldStorage _worldStorage;

        private readonly List<int3> _unloadedCoords = new List<int3>();
        private bool _initialized;

        public int PendingGenerationCount
        {
            get { return _generationScheduler?.PendingCount ?? 0; }
        }

        public int PendingMeshCount
        {
            get { return _meshScheduler?.PendingCount ?? 0; }
        }

        public int PendingLODMeshCount
        {
            get { return _lodScheduler?.PendingCount ?? 0; }
        }

        /// <summary>
        /// True once the spawn area chunks are fully meshed and the player has been placed.
        /// </summary>
        public bool SpawnReady
        {
            get { return _spawnManager != null && _spawnManager.IsComplete; }
        }

        public void Initialize(
            ChunkManager chunkManager,
            GenerationPipeline generationPipeline,
            NativeStateRegistry nativeStateRegistry,
            NativeAtlasLookup nativeAtlasLookup,
            ChunkMeshStore chunkMeshStore,
            DecorationStage decorationStage,
            WorldStorage worldStorage,
            long seed,
            ChunkSettings chunkSettings)
        {
            _chunkManager = chunkManager;
            _chunkMeshStore = chunkMeshStore;
            _worldStorage = worldStorage;

            _culling = new ChunkCulling();

            _generationScheduler = new GenerationScheduler(
                chunkManager,
                generationPipeline,
                decorationStage,
                worldStorage,
                nativeStateRegistry,
                seed,
                chunkSettings.MaxGenerationsPerFrame,
                chunkSettings.MaxGenCompletionsPerFrame,
                chunkSettings.MaxLightUpdatesPerFrame,
                chunkSettings.GenCompletionBudgetMs);
            _mainCamera = Camera.main;

            _meshScheduler = new MeshScheduler(
                chunkManager,
                nativeStateRegistry,
                nativeAtlasLookup,
                chunkMeshStore,
                _culling,
                chunkSettings.MaxMeshesPerFrame,
                chunkSettings.MaxMeshCompletionsPerFrame,
                chunkSettings.MeshCompletionBudgetMs);

            _lodScheduler = new LODScheduler(
                chunkManager,
                nativeStateRegistry,
                nativeAtlasLookup,
                chunkMeshStore,
                _culling,
                chunkSettings.MaxLODMeshesPerFrame,
                chunkSettings.MaxLODCompletionsPerFrame,
                chunkSettings.LodCompletionBudgetMs,
                chunkSettings.LOD1Distance,
                chunkSettings.LOD2Distance,
                chunkSettings.LOD3Distance);

            _initialized = true;
        }

        /// <summary>
        /// Sets the SpawnManager that coordinates spawn chunk loading and player placement.
        /// Must be called after Initialize.
        /// </summary>
        public void SetSpawnManager(SpawnManager spawnManager)
        {
            _spawnManager = spawnManager;
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            FrameProfiler.BeginFrame();
            PipelineStats.BeginFrame();
            FrameProfiler.Begin(FrameProfiler.UpdateTotal);

            FrameProfiler.Begin(FrameProfiler.PollGen);
            _generationScheduler.PollCompleted();
            FrameProfiler.End(FrameProfiler.PollGen);

            FrameProfiler.Begin(FrameProfiler.PollMesh);
            _meshScheduler.PollCompleted();
            FrameProfiler.End(FrameProfiler.PollMesh);

            FrameProfiler.Begin(FrameProfiler.PollLOD);
            _lodScheduler.PollCompleted();
            FrameProfiler.End(FrameProfiler.PollLOD);

            int3 cameraChunkCoord = GetCameraChunkCoord();

            _culling.UpdateFrustum(_mainCamera);

            FrameProfiler.Begin(FrameProfiler.LoadQueue);
            _chunkManager.UpdateLoadingQueue(cameraChunkCoord, (float3)_mainCamera.transform.forward);
            FrameProfiler.End(FrameProfiler.LoadQueue);

            FrameProfiler.Begin(FrameProfiler.Unload);
            _chunkManager.UnloadDistantChunks(cameraChunkCoord, _unloadedCoords, _worldStorage);

            // Advance spawn state machine until complete
            if (_spawnManager != null && !_spawnManager.IsComplete)
            {
                _spawnManager.Tick();
            }

            for (int i = 0; i < _unloadedCoords.Count; i++)
            {
                int3 coord = _unloadedCoords[i];

                // Force-complete any in-flight border extraction jobs that read this
                // chunk's NativeArray, so the pool can safely recycle it.
                _meshScheduler.ForceCompleteNeighborDeps(coord);

                _chunkMeshStore.DestroyRenderer(coord);
                _generationScheduler.CleanupCoord(coord);
                _meshScheduler.CleanupCoord(coord);
                _lodScheduler.CleanupCoord(coord);
            }
            FrameProfiler.End(FrameProfiler.Unload);

            FrameProfiler.Begin(FrameProfiler.SchedGen);
            _generationScheduler.ScheduleJobs();
            FrameProfiler.End(FrameProfiler.SchedGen);

            FrameProfiler.Begin(FrameProfiler.CrossLight);
            _generationScheduler.ProcessCrossChunkLightUpdates();
            FrameProfiler.End(FrameProfiler.CrossLight);

            FrameProfiler.Begin(FrameProfiler.Relight);
            _meshScheduler.ScheduleRelightJobs();
            FrameProfiler.End(FrameProfiler.Relight);

            // LOD level assignment must run BEFORE mesh scheduling
            // so chunks get their LOD level before MeshScheduler decides who to mesh
            FrameProfiler.Begin(FrameProfiler.LODLevels);
            _lodScheduler.UpdateLODLevels(cameraChunkCoord);
            FrameProfiler.End(FrameProfiler.LODLevels);

            FrameProfiler.Begin(FrameProfiler.SchedMesh);
            _meshScheduler.ScheduleJobs(SpawnReady);
            FrameProfiler.End(FrameProfiler.SchedMesh);

            FrameProfiler.Begin(FrameProfiler.SchedLOD);
            _lodScheduler.ScheduleJobs();
            FrameProfiler.End(FrameProfiler.SchedLOD);

            FrameProfiler.End(FrameProfiler.UpdateTotal);
        }

        private void LateUpdate()
        {
            if (!_initialized)
            {
                return;
            }

            FrameProfiler.Begin(FrameProfiler.Render);
            _chunkMeshStore.RenderAll(_mainCamera);
            FrameProfiler.End(FrameProfiler.Render);
        }

        private int3 GetCameraChunkCoord()
        {
            Vector3 camPos = _mainCamera != null ? _mainCamera.transform.position : Vector3.zero;

            return new int3(
                (int)math.floor(camPos.x / ChunkConstants.Size),
                (int)math.floor(camPos.y / ChunkConstants.Size),
                (int)math.floor(camPos.z / ChunkConstants.Size));
        }

        public void Shutdown()
        {
            _generationScheduler?.Shutdown();
            _meshScheduler?.Shutdown();
            _lodScheduler?.Shutdown();
        }
    }
}
