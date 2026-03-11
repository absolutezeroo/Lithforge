using System.Collections.Generic;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Content.Settings;
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
        private ChunkRenderManager _chunkRenderManager;
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
            ChunkRenderManager chunkRenderManager,
            DecorationStage decorationStage,
            WorldStorage worldStorage,
            long seed,
            ChunkSettings chunkSettings)
        {
            _chunkManager = chunkManager;
            _chunkRenderManager = chunkRenderManager;
            _worldStorage = worldStorage;

            _culling = new ChunkCulling();

            _generationScheduler = new GenerationScheduler(
                chunkManager,
                generationPipeline,
                decorationStage,
                worldStorage,
                nativeStateRegistry,
                seed,
                chunkSettings.MaxGenerationsPerFrame);
            _generationScheduler.SetCulling(_culling);

            _mainCamera = Camera.main;

            _meshScheduler = new MeshScheduler(
                chunkManager,
                nativeStateRegistry,
                nativeAtlasLookup,
                chunkRenderManager,
                _culling,
                chunkSettings.MaxMeshesPerFrame);

            _lodScheduler = new LODScheduler(
                chunkManager,
                nativeStateRegistry,
                nativeAtlasLookup,
                chunkRenderManager,
                _culling,
                chunkSettings.MaxLODMeshesPerFrame,
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

            _generationScheduler.PollCompleted();
            _meshScheduler.PollCompleted();
            _lodScheduler.PollCompleted();

            int3 cameraChunkCoord = GetCameraChunkCoord();

            _culling.UpdateFrustum(_mainCamera);

            _chunkManager.UpdateLoadingQueue(cameraChunkCoord);
            _chunkManager.UnloadDistantChunks(cameraChunkCoord, _unloadedCoords, _worldStorage);

            // Advance spawn state machine until complete
            if (_spawnManager != null && !_spawnManager.IsComplete)
            {
                _spawnManager.Tick();
            }

            for (int i = 0; i < _unloadedCoords.Count; i++)
            {
                int3 coord = _unloadedCoords[i];

                _chunkRenderManager.DestroyRenderer(coord);
                _generationScheduler.CleanupCoord(coord);
                _meshScheduler.CleanupCoord(coord);
                _lodScheduler.CleanupCoord(coord);
            }

            _generationScheduler.ScheduleJobs();
            _generationScheduler.ProcessCrossChunkLightUpdates();
            _meshScheduler.ProcessRelightPending();

            // LOD level assignment must run BEFORE mesh scheduling
            // so chunks get their LOD level before MeshScheduler decides who to mesh
            _lodScheduler.UpdateLODLevels(cameraChunkCoord);

            _meshScheduler.ScheduleJobs(SpawnReady);
            _lodScheduler.ScheduleJobs();
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
