using System;
using System.Collections.Generic;

using Lithforge.Meshing.Atlas;
using Lithforge.Network;
using Lithforge.Network.Connection;
using Lithforge.Runtime.Audio;
using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Player;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.Spawn;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.BlockEntity;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;
using Lithforge.WorldGen.Decoration;
using Lithforge.WorldGen.Pipeline;

using Unity.Burst;
using Unity.Mathematics;

using UnityEngine;
using UnityEngine.Profiling;

namespace Lithforge.Runtime
{
    public sealed class GameLoop : MonoBehaviour
    {
        private readonly List<int3> _networkUnloadCache = new();
        private readonly List<int3> _unloadedCoords = new();
        private GameLoopAudioState _audio;
        private AutoSaveManager _autoSaveManager;

        private BiomeTintManager _biomeTintManager;
        private BlockEntityTickScheduler _blockEntityTickScheduler;
        private BlockInteraction _blockInteraction;
        private ChunkManager _chunkManager;
        private ChunkMeshStore _chunkMeshStore;
        private ChunkCulling _culling;
        private GameLoopDebugState _debug = new();
        private GameState _gameState = GameState.Playing;
        private GenerationScheduler _generationScheduler;
        private GpuBufferResizer _gpuBufferResizer;
        private bool _initialized;

        // Liquid simulation
        private LiquidScheduler _liquidScheduler;
        private LODScheduler _lodScheduler;
        private Camera _mainCamera;
        private MeshScheduler _meshScheduler;
        private GameLoopNetworkState _network;
        private PlayerController _playerController;
        private PlayerRenderer _playerRenderer;
        private RelightScheduler _relightScheduler;
        private SpawnManager _spawnManager;
        private GameLoopTickState _tick;

        // Fixed tick rate system
        private TickAccumulator _tickAccumulator;
        private bool _clientDisconnectNotified;
        private float _unloadBudgetMs;
        private WorldStorage _worldStorage;

        /// <summary>
        ///     Invoked when a client-mode network connection transitions to Disconnected.
        ///     Wired by the bootstrap to trigger quit-to-title.
        /// </summary>
        public Action OnClientDisconnected;

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

        public int TicksThisFrame { get; private set; }

        /// <summary>
        ///     True once the spawn area chunks are fully meshed and the player has been placed.
        /// </summary>
        public bool SpawnReady
        {
            get { return _spawnManager != null && _spawnManager.IsComplete; }
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            _gpuBufferResizer?.Tick();

            _debug.FrameProfiler.BeginFrame();
            _debug.PipelineStats.BeginFrame();

            _debug.FrameProfiler.Begin(FrameProfilerSections.UpdateTotal);

            // ── Pump network transport (client mode) ──
            // Must run BEFORE the tick loop so transport events (Connect, HandshakeResponse)
            // are processed before gameplay ticks attempt to send messages.
            if (_network?.NetworkClient != null)
            {
                _network.NetworkClient.Update(Time.realtimeSinceStartup);

                // Detect client disconnection/timeout and return to menu
                if (_network.IsClientMode
                    && !_clientDisconnectNotified
                    && _network.NetworkClient.State == ConnectionState.Disconnected)
                {
                    _clientDisconnectNotified = true;
                    UnityEngine.Debug.LogWarning(
                        "[Lithforge] Client disconnected — returning to main menu.");
                    OnClientDisconnected?.Invoke();
                }
            }

            // ── Tick server game loop (host mode only) ──
            // Runs before tick loop so the host player's ticks see the latest server state.
            if (_network?.ServerGameLoop != null)
            {
                _network.ServerGameLoop.Update(Time.deltaTime, Time.realtimeSinceStartup);
            }

            // ── Fixed tick accumulator (skipped when fully paused) ──
            if (_tick?.WorldSimulation != null && _gameState != GameState.PausedFull)
            {
                _debug.FrameProfiler.Begin(FrameProfilerSections.TickLoop);
                Profiler.BeginSample("GL.TickLoop");

                // Update spawn-ready flag on physics body
                if (_tick.PlayerPhysicsBody != null)
                {
                    _tick.PlayerPhysicsBody.SpawnReady = SpawnReady;
                }

                // Latch edge-triggered inputs before the tick loop
                _tick.InputSnapshotBuilder.LatchFrame();

                _tickAccumulator.Accumulate(Time.deltaTime);

                TicksThisFrame = 0;

                while (_tickAccumulator.ShouldTick)
                {
                    _tick.WorldSimulation.Tick(FixedTickRate.TickDeltaTime);

                    _tickAccumulator.ConsumeOneTick();
                    TicksThisFrame++;
                }

                Profiler.EndSample();
                _debug.FrameProfiler.End(FrameProfilerSections.TickLoop);
            }

            // ── Tick remote player timeouts (client/host mode) ──
            if (_network?.RemotePlayerManager != null)
            {
                _network.RemotePlayerManager.Tick(Time.deltaTime);
            }

            // ── Async pipeline poll ──
            if (!(_network?.IsClientMode ?? false))
            {
                Profiler.BeginSample("GL.PollGen");
                _debug.FrameProfiler.Begin(FrameProfilerSections.PollGen);
                _generationScheduler.PollCompleted();
                _debug.FrameProfiler.End(FrameProfilerSections.PollGen);
                Profiler.EndSample();
            }

            Profiler.BeginSample("GL.PollRelight");
            _relightScheduler.PollCompleted();
            Profiler.EndSample();

            // PollMesh MUST run before PollLiquid: liquid result application calls
            // SetBlock which writes to chunk.Data. In-flight mesh jobs may be reading
            // that same Data via ExtractAllBordersJob (neighbor border slices).
            // Completing mesh jobs first releases those safety locks.
            Profiler.BeginSample("GL.PollMesh");
            _debug.FrameProfiler.Begin(FrameProfilerSections.PollMesh);
            _meshScheduler.PollCompleted();
            _debug.FrameProfiler.End(FrameProfilerSections.PollMesh);
            Profiler.EndSample();

            Profiler.BeginSample("GL.PollLiquid");
            _liquidScheduler?.PollCompleted();
            Profiler.EndSample();

            Profiler.BeginSample("GL.PollLOD");
            _debug.FrameProfiler.Begin(FrameProfilerSections.PollLOD);
            _lodScheduler.PollCompleted();
            _debug.FrameProfiler.End(FrameProfilerSections.PollLOD);
            Profiler.EndSample();

            // ── Frustum + load/unload (skipped when fully paused) ──
            int3 cameraChunkCoord = GetCameraChunkCoord();

            if (_gameState != GameState.PausedFull)
            {
                _culling.UpdateFrustum(_mainCamera);

                // Client mode: server manages chunk streaming, skip local load/unload
                if (!(_network?.IsClientMode ?? false))
                {
                    Profiler.BeginSample("GL.LoadQueue");
                    _debug.FrameProfiler.Begin(FrameProfilerSections.LoadQueue);
                    _chunkManager.UpdateLoadingQueue(cameraChunkCoord, _mainCamera.transform.forward);
                    _debug.FrameProfiler.End(FrameProfilerSections.LoadQueue);
                    Profiler.EndSample();
                }

                Profiler.BeginSample("GL.Unload");
                _debug.FrameProfiler.Begin(FrameProfilerSections.Unload);
                if (!(_network?.IsClientMode ?? false))
                {
                    _chunkManager.UnloadDistantChunks(
                        cameraChunkCoord, _unloadedCoords, _worldStorage, _unloadBudgetMs);
                }

                // Drain network-driven chunk unloads (client mode)
                if (_network?.ClientChunkHandler != null)
                {
                    _network.ClientChunkHandler.DrainPendingUnloads(_networkUnloadCache);

                    for (int i = 0; i < _networkUnloadCache.Count; i++)
                    {
                        int3 coord = _networkUnloadCache[i];
                        _chunkManager.UnloadChunk(coord);
                        _unloadedCoords.Add(coord);
                    }
                }

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

                    // Return liquid data to pool for the unloaded chunk
                    _liquidScheduler?.OnChunkUnloaded(coord);

                    _chunkMeshStore.DestroyRenderer(coord);
                    _biomeTintManager?.OnChunkUnloaded(coord);
                    _generationScheduler.CleanupCoord(coord);
                    _relightScheduler.CleanupCoord(coord);
                    _meshScheduler.CleanupCoord(coord);
                    _lodScheduler.CleanupCoord(coord);
                }
                _debug.FrameProfiler.End(FrameProfilerSections.Unload);
                Profiler.EndSample();
            }

            // ── Schedule jobs (skipped when fully paused) ──
            if (_gameState != GameState.PausedFull)
            {
                // Client mode: server generates chunks, skip local generation scheduling
                if (!(_network?.IsClientMode ?? false))
                {
                    Profiler.BeginSample("GL.SchedGen");
                    _debug.FrameProfiler.Begin(FrameProfilerSections.SchedGen);
                    _generationScheduler.ScheduleJobs();
                    _debug.FrameProfiler.End(FrameProfilerSections.SchedGen);
                    Profiler.EndSample();

                    Profiler.BeginSample("GL.CrossLight");
                    _debug.FrameProfiler.Begin(FrameProfilerSections.CrossLight);
                    _generationScheduler.ProcessCrossChunkLightUpdates();
                    _debug.FrameProfiler.End(FrameProfilerSections.CrossLight);
                    Profiler.EndSample();
                }

                Profiler.BeginSample("GL.Relight");
                _debug.FrameProfiler.Begin(FrameProfilerSections.Relight);
                _relightScheduler.ScheduleJobs();
                _debug.FrameProfiler.End(FrameProfilerSections.Relight);
                Profiler.EndSample();

                // LOD level assignment must run BEFORE mesh scheduling
                // so chunks get their LOD level before MeshScheduler decides who to mesh
                Profiler.BeginSample("GL.LODLevels");
                _debug.FrameProfiler.Begin(FrameProfilerSections.LODLevels);
                _lodScheduler.UpdateLODLevels(cameraChunkCoord);
                _debug.FrameProfiler.End(FrameProfilerSections.LODLevels);
                Profiler.EndSample();

                Profiler.BeginSample("GL.SchedMesh");
                _debug.FrameProfiler.Begin(FrameProfilerSections.SchedMesh);
                float3 camForwardXZ = math.normalizesafe(
                    new float3(_mainCamera.transform.forward.x, 0, _mainCamera.transform.forward.z));
                _meshScheduler.ScheduleJobs(SpawnReady, cameraChunkCoord, camForwardXZ);
                _debug.FrameProfiler.End(FrameProfilerSections.SchedMesh);
                Profiler.EndSample();

                Profiler.BeginSample("GL.SchedLOD");
                _debug.FrameProfiler.Begin(FrameProfilerSections.SchedLOD);
                _lodScheduler.ScheduleJobs();
                _debug.FrameProfiler.End(FrameProfilerSections.SchedLOD);
                Profiler.EndSample();
            }

            // Audio environment frame-rate updates (filter/reverb smoothing, crossfade)
            if (_audio?.AudioEnvironmentController != null)
            {
                _audio.AudioEnvironmentController.UpdateFrame(Time.deltaTime);
            }

            // Auto-save: periodic metadata + dirty chunk flush
            if (_autoSaveManager != null)
            {
                _autoSaveManager.Tick(Time.realtimeSinceStartup);
            }

            _debug.FrameProfiler.End(FrameProfilerSections.UpdateTotal);

            // CommitFrame AFTER all systems have run so the snapshot captures
            // incremented PipelineStats counters and completed FrameProfiler sections.
            if (_debug.MetricsRegistry != null)
            {
                _debug.MetricsRegistry.CommitFrame();
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
            if (_tick?.PlayerPhysicsBody != null && _tick.PlayerTransform != null
                                                 && !_tick.PlayerPhysicsBody.ExternallyControlled)
            {
                float alpha = _tickAccumulator.Alpha;
                float3 interpPos = math.lerp(
                    _tick.PlayerPhysicsBody.PreviousPosition,
                    _tick.PlayerPhysicsBody.CurrentPosition,
                    alpha);

                // Apply reconciliation position error offset (client mode)
                if (_tick.WorldSimulation is ClientWorldSimulation clientSim)
                {
                    interpPos += clientSim.PositionError;
                }

                _tick.PlayerTransform.position = new Vector3(interpPos.x, interpPos.y, interpPos.z);
            }

            // Footstep and fall detection (after interpolation, before render)
            if (_audio?.FootstepController != null)
            {
                _audio.FootstepController.Update();
            }

            if (_audio?.FallSoundDetector != null)
            {
                _audio.FallSoundDetector.Update();
            }

            // Release finished SFX sources
            if (_audio?.SfxSourcePool != null)
            {
                _audio.SfxSourcePool.ReleaseFinished();
            }

            _debug.FrameProfiler.Begin(FrameProfilerSections.Render);
            _chunkMeshStore.RenderAll(_mainCamera);

            if (_playerRenderer != null)
            {
                _playerRenderer.Render(
                    _playerController != null && _playerController.OnGround,
                    _playerController != null && _playerController.IsFlying,
                    _blockInteraction != null && _blockInteraction.IsMining);
            }

            if (_network?.RemotePlayerManager != null)
            {
                _network.RemotePlayerManager.RenderAll(_mainCamera);
            }

            _debug.FrameProfiler.End(FrameProfilerSections.Render);
        }

        public void SetDebugState(GameLoopDebugState debug)
        {
            _debug = debug ?? new GameLoopDebugState();
        }

        public void SetNetworkState(GameLoopNetworkState network)
        {
            _network = network;
        }

        public void SetAudioState(GameLoopAudioState audio)
        {
            _audio = audio;
        }

        public void SetTickState(GameLoopTickState tick)
        {
            _tick = tick;
        }

        /// <summary>
        ///     Sets the GPU buffer resizer for deferred disposal ticking.
        ///     Must be called before Initialize.
        /// </summary>
        public void SetGpuBufferResizer(GpuBufferResizer resizer)
        {
            _gpuBufferResizer = resizer;
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
                _debug.PipelineStats,
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
                _debug.PipelineStats,
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
                _debug.PipelineStats,
                SchedulingConfig.MaxLODMeshesPerFrame(rd),
                SchedulingConfig.MaxLODCompletionsPerFrame(rd),
                chunkSettings.LodCompletionBudgetMs,
                SchedulingConfig.LOD1Distance(rd),
                SchedulingConfig.LOD2Distance(rd),
                SchedulingConfig.LOD3Distance(rd));

            _initialized = true;

#if UNITY_EDITOR
            if (!BurstCompiler.IsEnabled)
            {
                UnityEngine.Debug.LogError(
                    "[Lithforge] Burst is DISABLED — lighting performance will be severely degraded. " +
                    "Enable via Jobs > Burst > Enable Compilation.");
            }
#endif
        }

        /// <summary>
        ///     Sets the SpawnManager that coordinates spawn chunk loading and player placement.
        ///     Must be called after Initialize.
        /// </summary>
        public void SetSpawnManager(SpawnManager spawnManager)
        {
            _spawnManager = spawnManager;
        }

        /// <summary>
        ///     Sets the BiomeTintManager for climate data streaming.
        ///     Must be called after Initialize.
        /// </summary>
        public void SetBiomeTintManager(BiomeTintManager biomeTintManager)
        {
            _biomeTintManager = biomeTintManager;
            _generationScheduler.SetBiomeTintManager(biomeTintManager);
        }

        /// <summary>
        ///     Sets the BlockEntityTickScheduler for ticking and unload cleanup.
        ///     Wires the generation scheduler's entity load delegate.
        ///     Must be called after Initialize.
        /// </summary>
        public void SetBlockEntityTickScheduler(BlockEntityTickScheduler scheduler)
        {
            _blockEntityTickScheduler = scheduler;
            _generationScheduler.OnChunkEntitiesLoaded += scheduler.RegisterEntitiesForChunk;
        }

        /// <summary>
        ///     Sets the BlockEntityRegistry on the generation scheduler for chunk deserialization.
        ///     Must be called after Initialize.
        /// </summary>
        public void SetBlockEntityRegistry(BlockEntityRegistry registry)
        {
            _generationScheduler.SetBlockEntityRegistry(registry);
        }

        /// <summary>
        ///     Sets the liquid scheduler for liquid simulation polling and chunk unload cleanup.
        ///     Also wires it to the generation scheduler for InitChunkLiquid on newly generated chunks.
        ///     Must be called after Initialize.
        /// </summary>
        public void SetLiquidScheduler(LiquidScheduler liquidScheduler)
        {
            _liquidScheduler = liquidScheduler;
            _generationScheduler.SetLiquidScheduler(liquidScheduler);
            liquidScheduler.SetMeshScheduler(_meshScheduler);

            // Wire block changes to liquid scheduler so breaking/placing wakes flow
            _chunkManager.OnBlockChanged += liquidScheduler.OnBlockChanged;
        }

        /// <summary>
        ///     Sets the player model renderer and player references for body rendering.
        ///     Must be called after Initialize.
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
        ///     Recalibrates all schedulers when render distance changes at runtime.
        ///     Called by SettingsScreen after SetRenderDistance.
        /// </summary>
        public void NotifyRenderDistanceChanged(int renderDistance)
        {
            _generationScheduler.UpdateConfig(renderDistance);
            _meshScheduler.UpdateConfig(renderDistance);
            _lodScheduler.UpdateConfig(renderDistance);
        }


        private int3 GetCameraChunkCoord()
        {
            Vector3 camPos = _mainCamera != null ? _mainCamera.transform.position : Vector3.zero;

            return new int3(
                (int)math.floor(camPos.x / ChunkConstants.Size),
                (int)math.floor(camPos.y / ChunkConstants.Size),
                (int)math.floor(camPos.z / ChunkConstants.Size));
        }

        /// <summary>
        ///     Sets the game state, which gates the tick loop and job scheduling.
        ///     PausedFull stops ticks and scheduling; PausedOverlay allows them to continue.
        /// </summary>
        public void SetGameState(GameState state)
        {
            _gameState = state;
        }

        public void Shutdown()
        {
            _liquidScheduler?.Shutdown();
            _generationScheduler?.Shutdown();
            _relightScheduler?.Shutdown();
            _meshScheduler?.Shutdown();
            _lodScheduler?.Shutdown();
            _playerRenderer?.Dispose();
            _playerRenderer = null;

            if (_network != null)
            {
                _network.RemotePlayerManager?.Dispose();
                _network.RemotePlayerManager = null;
            }
        }
    }
}
