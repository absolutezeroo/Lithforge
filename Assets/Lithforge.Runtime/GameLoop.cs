using System.Collections.Generic;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Player;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.Spawn;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;
using Lithforge.WorldGen.Decoration;
using Lithforge.WorldGen.Pipeline;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Lithforge.Runtime
{
    public sealed class GameLoop : MonoBehaviour
    {
        private ChunkManager _chunkManager;
        private ChunkMeshStore _chunkMeshStore;
        private GenerationScheduler _generationScheduler;
        private MeshScheduler _meshScheduler;
        private RelightScheduler _relightScheduler;
        private LODScheduler _lodScheduler;
        private ChunkCulling _culling;
        private SpawnManager _spawnManager;
        private Camera _mainCamera;
        private WorldStorage _worldStorage;

        private BiomeTintManager _biomeTintManager;
        private BlockEntityTickScheduler _blockEntityTickScheduler;
        private PlayerRenderer _playerRenderer;
        private PlayerController _playerController;
        private BlockInteraction _blockInteraction;
        private AutoSaveManager _autoSaveManager;
        private Debug.MetricsRegistry _metricsRegistry;
        private readonly List<int3> _unloadedCoords = new List<int3>();
        private float _unloadBudgetMs;
        private bool _initialized;

        // Fixed tick rate system
        private TickAccumulator _tickAccumulator;
        private TickRegistry _tickRegistry;
        private PlayerInputLatch _playerInputLatch;
        private PlayerPhysicsBody _playerPhysicsBody;
        private Transform _playerTransform;
        private int _ticksThisFrame;

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

        public int TicksThisFrame
        {
            get { return _ticksThisFrame; }
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
            _unloadBudgetMs = chunkSettings.UnloadBudgetMs;

            _culling = new ChunkCulling();

            int rd = chunkSettings.RenderDistance;

            _generationScheduler = new GenerationScheduler(
                chunkManager,
                generationPipeline,
                decorationStage,
                worldStorage,
                nativeStateRegistry,
                seed,
                SchedulingConfig.MaxGenerationsPerFrame(rd),
                SchedulingConfig.MaxGenCompletionsPerFrame(rd),
                chunkSettings.MaxLightUpdatesPerFrame,
                chunkSettings.GenCompletionBudgetMs);
            _mainCamera = Camera.main;

            _meshScheduler = new MeshScheduler(
                chunkManager,
                nativeStateRegistry,
                nativeAtlasLookup,
                chunkMeshStore,
                _culling,
                SchedulingConfig.MaxMeshesPerFrame(rd),
                SchedulingConfig.MaxMeshCompletionsPerFrame(rd),
                chunkSettings.MeshCompletionBudgetMs);
            _meshScheduler.UpdateConfig(rd);

            _relightScheduler = new RelightScheduler(
                chunkManager,
                nativeStateRegistry);

            _lodScheduler = new LODScheduler(
                chunkManager,
                nativeStateRegistry,
                nativeAtlasLookup,
                chunkMeshStore,
                _culling,
                SchedulingConfig.MaxLODMeshesPerFrame(rd),
                SchedulingConfig.MaxLODCompletionsPerFrame(rd),
                chunkSettings.LodCompletionBudgetMs,
                SchedulingConfig.LOD1Distance(rd),
                SchedulingConfig.LOD2Distance(rd),
                SchedulingConfig.LOD3Distance(rd));

            _initialized = true;

#if UNITY_EDITOR
            if (!Unity.Burst.BurstCompiler.IsEnabled)
            {
                UnityEngine.Debug.LogError(
                    "[Lithforge] Burst is DISABLED — lighting performance will be severely degraded. " +
                    "Enable via Jobs > Burst > Enable Compilation.");
            }
#endif
        }

        /// <summary>
        /// Sets the SpawnManager that coordinates spawn chunk loading and player placement.
        /// Must be called after Initialize.
        /// </summary>
        public void SetSpawnManager(SpawnManager spawnManager)
        {
            _spawnManager = spawnManager;
        }

        /// <summary>
        /// Sets the BiomeTintManager for climate data streaming.
        /// Must be called after Initialize.
        /// </summary>
        public void SetBiomeTintManager(BiomeTintManager biomeTintManager)
        {
            _biomeTintManager = biomeTintManager;
            _generationScheduler.SetBiomeTintManager(biomeTintManager);
        }

        /// <summary>
        /// Sets the BlockEntityTickScheduler for ticking and unload cleanup.
        /// Wires the generation scheduler's entity load delegate.
        /// Must be called after Initialize.
        /// </summary>
        public void SetBlockEntityTickScheduler(BlockEntityTickScheduler scheduler)
        {
            _blockEntityTickScheduler = scheduler;
            _generationScheduler.OnChunkEntitiesLoaded += scheduler.RegisterEntitiesForChunk;
        }

        /// <summary>
        /// Sets the BlockEntityRegistry on the generation scheduler for chunk deserialization.
        /// Must be called after Initialize.
        /// </summary>
        public void SetBlockEntityRegistry(Lithforge.Voxel.BlockEntity.BlockEntityRegistry registry)
        {
            _generationScheduler.SetBlockEntityRegistry(registry);
        }

        /// <summary>
        /// Sets the player model renderer and player references for body rendering.
        /// Must be called after Initialize.
        /// </summary>
        public void SetPlayerRenderer(
            PlayerRenderer playerRenderer,
            PlayerController playerController,
            BlockInteraction blockInteraction)
        {
            _playerRenderer = playerRenderer;
            _playerController = playerController;
            _blockInteraction = blockInteraction;
        }

        public void SetAutoSaveManager(AutoSaveManager autoSaveManager)
        {
            _autoSaveManager = autoSaveManager;
        }

        /// <summary>
        /// Sets the MetricsRegistry for per-frame diagnostic aggregation.
        /// CommitFrame() is called at the end of Update, after all systems have run.
        /// </summary>
        public void SetMetricsRegistry(Debug.MetricsRegistry metricsRegistry)
        {
            _metricsRegistry = metricsRegistry;
        }

        /// <summary>
        /// Recalibrates all schedulers when render distance changes at runtime.
        /// Called by SettingsScreen after SetRenderDistance.
        /// </summary>
        public void NotifyRenderDistanceChanged(int renderDistance)
        {
            _generationScheduler.UpdateConfig(renderDistance);
            _meshScheduler.UpdateConfig(renderDistance);
            _lodScheduler.UpdateConfig(renderDistance);
        }

        /// <summary>
        /// Wires the fixed tick rate systems. Must be called after Initialize.
        /// </summary>
        public void SetTickSystems(
            TickRegistry tickRegistry,
            PlayerInputLatch playerInputLatch,
            PlayerPhysicsBody playerPhysicsBody,
            Transform playerTransform)
        {
            _tickRegistry = tickRegistry;
            _playerInputLatch = playerInputLatch;
            _playerPhysicsBody = playerPhysicsBody;
            _playerTransform = playerTransform;
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

            // ── Fixed tick accumulator ──
            if (_tickRegistry != null)
            {
                FrameProfiler.Begin(FrameProfiler.TickLoop);
                Profiler.BeginSample("GL.TickLoop");

                // Update spawn-ready flag on physics body
                if (_playerPhysicsBody != null)
                {
                    _playerPhysicsBody.SpawnReady = SpawnReady;
                }

                // Latch edge-triggered inputs before the tick loop
                _playerInputLatch.LatchFrame();

                _tickAccumulator.Accumulate(Time.deltaTime);

                _ticksThisFrame = 0;

                while (_tickAccumulator.ShouldTick)
                {
                    InputLatchSnapshot latch = _playerInputLatch.ConsumeTick();

                    if (_playerPhysicsBody != null)
                    {
                        _playerPhysicsBody.TickWithLatch(
                            FixedTickRate.TickDeltaTime, in latch);
                    }

                    _tickRegistry.TickAll(FixedTickRate.TickDeltaTime);

                    _tickAccumulator.ConsumeOneTick();
                    _ticksThisFrame++;
                }

                Profiler.EndSample();
                FrameProfiler.End(FrameProfiler.TickLoop);
            }

            // ── Async pipeline poll ──
            Profiler.BeginSample("GL.PollGen");
            FrameProfiler.Begin(FrameProfiler.PollGen);
            _generationScheduler.PollCompleted();
            FrameProfiler.End(FrameProfiler.PollGen);
            Profiler.EndSample();

            Profiler.BeginSample("GL.PollRelight");
            _relightScheduler.PollCompleted();
            Profiler.EndSample();

            Profiler.BeginSample("GL.PollMesh");
            FrameProfiler.Begin(FrameProfiler.PollMesh);
            _meshScheduler.PollCompleted();
            FrameProfiler.End(FrameProfiler.PollMesh);
            Profiler.EndSample();

            Profiler.BeginSample("GL.PollLOD");
            FrameProfiler.Begin(FrameProfiler.PollLOD);
            _lodScheduler.PollCompleted();
            FrameProfiler.End(FrameProfiler.PollLOD);
            Profiler.EndSample();

            // ── Frustum + load/unload ──
            int3 cameraChunkCoord = GetCameraChunkCoord();

            _culling.UpdateFrustum(_mainCamera);

            Profiler.BeginSample("GL.LoadQueue");
            FrameProfiler.Begin(FrameProfiler.LoadQueue);
            _chunkManager.UpdateLoadingQueue(cameraChunkCoord, (float3)_mainCamera.transform.forward);
            FrameProfiler.End(FrameProfiler.LoadQueue);
            Profiler.EndSample();

            Profiler.BeginSample("GL.Unload");
            FrameProfiler.Begin(FrameProfiler.Unload);
            _chunkManager.UnloadDistantChunks(
                cameraChunkCoord, _unloadedCoords, _worldStorage, _unloadBudgetMs);

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

                // Clean up block entity scheduler tracking for the unloaded chunk
                _blockEntityTickScheduler?.OnChunkUnloaded(coord);

                _chunkMeshStore.DestroyRenderer(coord);
                _biomeTintManager?.OnChunkUnloaded(coord);
                _generationScheduler.CleanupCoord(coord);
                _relightScheduler.CleanupCoord(coord);
                _meshScheduler.CleanupCoord(coord);
                _lodScheduler.CleanupCoord(coord);
            }
            FrameProfiler.End(FrameProfiler.Unload);
            Profiler.EndSample();

            // ── Schedule jobs ──
            Profiler.BeginSample("GL.SchedGen");
            FrameProfiler.Begin(FrameProfiler.SchedGen);
            _generationScheduler.ScheduleJobs();
            FrameProfiler.End(FrameProfiler.SchedGen);
            Profiler.EndSample();

            Profiler.BeginSample("GL.CrossLight");
            FrameProfiler.Begin(FrameProfiler.CrossLight);
            _generationScheduler.ProcessCrossChunkLightUpdates();
            FrameProfiler.End(FrameProfiler.CrossLight);
            Profiler.EndSample();

            Profiler.BeginSample("GL.Relight");
            FrameProfiler.Begin(FrameProfiler.Relight);
            _relightScheduler.ScheduleJobs();
            FrameProfiler.End(FrameProfiler.Relight);
            Profiler.EndSample();

            // LOD level assignment must run BEFORE mesh scheduling
            // so chunks get their LOD level before MeshScheduler decides who to mesh
            Profiler.BeginSample("GL.LODLevels");
            FrameProfiler.Begin(FrameProfiler.LODLevels);
            _lodScheduler.UpdateLODLevels(cameraChunkCoord);
            FrameProfiler.End(FrameProfiler.LODLevels);
            Profiler.EndSample();

            Profiler.BeginSample("GL.SchedMesh");
            FrameProfiler.Begin(FrameProfiler.SchedMesh);
            _meshScheduler.ScheduleJobs(SpawnReady);
            FrameProfiler.End(FrameProfiler.SchedMesh);
            Profiler.EndSample();

            Profiler.BeginSample("GL.SchedLOD");
            FrameProfiler.Begin(FrameProfiler.SchedLOD);
            _lodScheduler.ScheduleJobs();
            FrameProfiler.End(FrameProfiler.SchedLOD);
            Profiler.EndSample();

            // Auto-save: periodic metadata + dirty chunk flush
            if (_autoSaveManager != null)
            {
                _autoSaveManager.Tick(Time.realtimeSinceStartup);
            }

            FrameProfiler.End(FrameProfiler.UpdateTotal);

            // CommitFrame AFTER all systems have run so the snapshot captures
            // incremented PipelineStats counters and completed FrameProfiler sections.
            if (_metricsRegistry != null)
            {
                _metricsRegistry.CommitFrame();
            }
        }

        private void LateUpdate()
        {
            if (!_initialized)
            {
                return;
            }

            // Apply interpolated player position for smooth rendering.
            // Skip when externally controlled (e.g. BenchmarkRunner moves transform directly).
            if (_playerPhysicsBody != null && _playerTransform != null
                && !_playerPhysicsBody.ExternallyControlled)
            {
                float alpha = _tickAccumulator.Alpha;
                float3 interpPos = math.lerp(
                    _playerPhysicsBody.PreviousPosition,
                    _playerPhysicsBody.CurrentPosition,
                    alpha);
                _playerTransform.position = new Vector3(interpPos.x, interpPos.y, interpPos.z);
            }

            FrameProfiler.Begin(FrameProfiler.Render);
            _chunkMeshStore.RenderAll(_mainCamera);

            if (_playerRenderer != null)
            {
                _playerRenderer.Render(
                    _playerController != null && _playerController.OnGround,
                    _playerController != null && _playerController.IsFlying,
                    _blockInteraction != null && _blockInteraction.IsMining);
            }

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
            _relightScheduler?.Shutdown();
            _meshScheduler?.Shutdown();
            _lodScheduler?.Shutdown();
            _playerRenderer?.Dispose();
        }
    }
}
