using System;
using System.Collections.Generic;

using Lithforge.Network;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.Tick;
using Lithforge.Voxel.Chunk;

using Unity.Mathematics;

using UnityEngine;
using UnityEngine.Profiling;

namespace Lithforge.Runtime.Session
{
    /// <summary>
    ///     Pure C# replacement for the <c>GameLoop</c> MonoBehaviour.
    ///     Called each frame by <see cref="SessionBridge" />.
    ///     In always-server mode, all modes (SP/Host/Client) use NetworkClient + ClientWorldSimulation.
    ///     Server-side work (generation, loading) is handled by <see cref="ServerLoopPoco" />.
    /// </summary>
    public sealed class GameLoopPoco
    {
        private readonly GameLoopConfig _config;

        private readonly List<int3> _networkUnloadCache = new();

        private readonly List<int3> _unloadedCoords = new();

        private bool _clientDisconnectNotified;

        private GameState _gameState = GameState.Playing;

        private TickAccumulator _tickAccumulator;

        /// <summary>
        ///     Invoked when the network connection transitions to Disconnected.
        /// </summary>
        public Action OnClientDisconnected;

        public GameLoopPoco(GameLoopConfig config)
        {
            _config = config;
        }

        public int PendingGenerationCount
        {
            get { return _config.ServerLoop?.PendingGenerationCount ?? 0; }
        }

        public int PendingMeshCount
        {
            get { return _config.MeshScheduler?.PendingCount ?? 0; }
        }

        public int PendingLODMeshCount
        {
            get { return _config.LODScheduler?.PendingCount ?? 0; }
        }

        public int TicksThisFrame { get; private set; }

        public bool SpawnReady
        {
            get
            {
                // All modes: SpawnReady is set on the physics body
                // by the GameReady handler in ClientChunkHandlerSubsystem
                return _config.PlayerPhysicsBody != null
                    && _config.PlayerPhysicsBody.SpawnReady;
            }
        }

        public void SetGameState(GameState state)
        {
            _gameState = state;
        }

        /// <summary>
        ///     Sets the world simulation after deferred initialization.
        ///     In always-server mode, <see cref="ClientWorldSimulation" /> is created
        ///     when the handshake completes (after PostInitialize), so this method
        ///     allows the callback to wire it into the running game loop.
        /// </summary>
        public void SetWorldSimulation(IWorldSimulation worldSimulation)
        {
            _config.WorldSimulation = worldSimulation;
        }

        public void NotifyRenderDistanceChanged(int renderDistance)
        {
            _config.ServerLoop?.NotifyRenderDistanceChanged(renderDistance);
            _config.MeshScheduler?.UpdateConfig(renderDistance);
            _config.LODScheduler?.UpdateConfig(renderDistance);
        }

        public void Update()
        {
            _config.GpuBufferResizer?.Tick();

            IFrameProfiler profiler = _config.FrameProfiler ?? new NullFrameProfiler();
            IPipelineStats stats = _config.PipelineStats ?? new NullPipelineStats();

            profiler.BeginFrame();
            stats.BeginFrame();

            profiler.Begin(FrameProfilerSections.UpdateTotal);

            // ── Poll server-side generation completions ──
            if (_config.ServerLoop != null)
            {
                profiler.Begin(FrameProfilerSections.PollGen);
                _config.ServerLoop.PollCompletions();
                profiler.End(FrameProfilerSections.PollGen);
            }

            // ── Pump network client (always present in SP/Host/Client) ──
            if (_config.NetworkClient != null)
            {
                _config.NetworkClient.Update(Time.realtimeSinceStartup);

                if (!_clientDisconnectNotified
                    && _config.NetworkClient.State == ConnectionState.Disconnected)
                {
                    _clientDisconnectNotified = true;
                    UnityEngine.Debug.LogWarning(
                        "[Lithforge] Client disconnected — returning to main menu.");
                    OnClientDisconnected?.Invoke();
                }
            }

            // ── Tick server game loop (SP + Host) ──
            if (_config.ServerGameLoop != null)
            {
                _config.ServerGameLoop.Update(Time.deltaTime, Time.realtimeSinceStartup);
            }

            // ── Fixed tick accumulator (skipped when fully paused) ──
            if (_config.WorldSimulation != null && _gameState != GameState.PausedFull)
            {
                profiler.Begin(FrameProfilerSections.TickLoop);
                Profiler.BeginSample("GL.TickLoop");

                if (_config.PlayerPhysicsBody != null)
                {
                    _config.PlayerPhysicsBody.SpawnReady = SpawnReady;
                }

                _config.InputSnapshotBuilder?.LatchFrame();

                _tickAccumulator.Accumulate(Time.deltaTime);

                TicksThisFrame = 0;

                while (_tickAccumulator.ShouldTick)
                {
                    _config.WorldSimulation.Tick(FixedTickRate.TickDeltaTime);
                    _tickAccumulator.ConsumeOneTick();
                    TicksThisFrame++;
                }

                Profiler.EndSample();
                profiler.End(FrameProfilerSections.TickLoop);
            }

            // ── Tick remote player timeouts ──
            if (_config.RemotePlayerManager != null)
            {
                _config.RemotePlayerManager.Tick(Time.deltaTime);
            }

            // ── Async pipeline poll (rendering-side schedulers) ──
            Profiler.BeginSample("GL.PollRelight");
            _config.RelightScheduler?.PollCompleted();
            Profiler.EndSample();

            Profiler.BeginSample("GL.PollMesh");
            profiler.Begin(FrameProfilerSections.PollMesh);
            _config.MeshScheduler?.PollCompleted();
            profiler.End(FrameProfilerSections.PollMesh);
            Profiler.EndSample();

            Profiler.BeginSample("GL.PollLiquid");
            _config.LiquidScheduler?.PollCompleted();
            Profiler.EndSample();

            Profiler.BeginSample("GL.PollLOD");
            profiler.Begin(FrameProfilerSections.PollLOD);
            _config.LODScheduler?.PollCompleted();
            profiler.End(FrameProfilerSections.PollLOD);
            Profiler.EndSample();

            // ── Frustum + load/unload ──
            int3 cameraChunkCoord = GetCameraChunkCoord();

            if (_gameState != GameState.PausedFull)
            {
                _config.Culling?.UpdateFrustum(_config.MainCamera);

                // Server-side loading and unloading (SP + Host)
                if (_config.ServerLoop != null)
                {
                    profiler.Begin(FrameProfilerSections.LoadQueue);
                    _config.ServerLoop.UpdateLoadingAndUnloading();
                    profiler.End(FrameProfilerSections.LoadQueue);

                    // Collect server-side unloaded coords
                    IReadOnlyList<int3> serverUnloads = _config.ServerLoop.UnloadedCoords;

                    for (int i = 0; i < serverUnloads.Count; i++)
                    {
                        _unloadedCoords.Add(serverUnloads[i]);
                    }
                }

                // Client-side chunk unloads (SP + Host + Client)
                Profiler.BeginSample("GL.Unload");
                profiler.Begin(FrameProfilerSections.Unload);

                if (_config.ClientChunkHandler != null)
                {
                    _config.ClientChunkHandler.DrainPendingUnloads(_networkUnloadCache);

                    for (int i = 0; i < _networkUnloadCache.Count; i++)
                    {
                        int3 coord = _networkUnloadCache[i];
                        _config.ChunkManager.UnloadChunk(coord);
                        _unloadedCoords.Add(coord);
                    }
                }

                // Cleanup unloaded coords from all rendering-side subsystems
                for (int i = 0; i < _unloadedCoords.Count; i++)
                {
                    int3 coord = _unloadedCoords[i];
                    _config.MeshScheduler?.ForceCompleteNeighborDeps(coord);
                    _config.BlockEntityTickScheduler?.OnChunkUnloaded(coord);
                    _config.LiquidScheduler?.OnChunkUnloaded(coord);
                    _config.ChunkMeshStore?.DestroyRenderer(coord);
                    _config.BiomeTintManager?.OnChunkUnloaded(coord);
                    _config.RelightScheduler?.CleanupCoord(coord);
                    _config.MeshScheduler?.CleanupCoord(coord);
                    _config.LODScheduler?.CleanupCoord(coord);
                }

                _unloadedCoords.Clear();

                profiler.End(FrameProfilerSections.Unload);
                Profiler.EndSample();
            }

            // ── Schedule jobs ──
            if (_gameState != GameState.PausedFull)
            {
                // Server-side generation scheduling (SP + Host)
                if (_config.ServerLoop != null)
                {
                    profiler.Begin(FrameProfilerSections.SchedGen);
                    _config.ServerLoop.ScheduleJobs();
                    profiler.End(FrameProfilerSections.SchedGen);
                }

                Profiler.BeginSample("GL.Relight");
                profiler.Begin(FrameProfilerSections.Relight);
                _config.RelightScheduler?.ScheduleJobs();
                profiler.End(FrameProfilerSections.Relight);
                Profiler.EndSample();

                Profiler.BeginSample("GL.LODLevels");
                profiler.Begin(FrameProfilerSections.LODLevels);
                _config.LODScheduler?.UpdateLODLevels(cameraChunkCoord);
                profiler.End(FrameProfilerSections.LODLevels);
                Profiler.EndSample();

                Profiler.BeginSample("GL.SchedMesh");
                profiler.Begin(FrameProfilerSections.SchedMesh);
                float3 camForwardXZ = math.normalizesafe(
                    new float3(
                        _config.MainCamera.transform.forward.x,
                        0,
                        _config.MainCamera.transform.forward.z));
                _config.MeshScheduler?.ScheduleJobs(SpawnReady, cameraChunkCoord, camForwardXZ);
                profiler.End(FrameProfilerSections.SchedMesh);
                Profiler.EndSample();

                Profiler.BeginSample("GL.SchedLOD");
                profiler.Begin(FrameProfilerSections.SchedLOD);
                _config.LODScheduler?.ScheduleJobs();
                profiler.End(FrameProfilerSections.SchedLOD);
                Profiler.EndSample();
            }

            // Audio environment frame-rate updates
            _config.AudioEnvironmentController?.UpdateFrame(Time.deltaTime);

            // Server-side gameplay ticking (SP + Host)
            _config.ServerLoop?.TickGameplay(Time.realtimeSinceStartup);

            profiler.End(FrameProfilerSections.UpdateTotal);

            // CommitFrame AFTER all systems have run
            _config.MetricsRegistry?.CommitFrame();
        }

        public void LateUpdate()
        {
            IFrameProfiler profiler = _config.FrameProfiler ?? new NullFrameProfiler();

            // Apply interpolated player position
            if (_config.PlayerPhysicsBody != null && _config.PlayerTransform != null
                                                  && !_config.PlayerPhysicsBody.ExternallyControlled)
            {
                float alpha = _tickAccumulator.Alpha;
                float3 interpPos = math.lerp(
                    _config.PlayerPhysicsBody.PreviousPosition,
                    _config.PlayerPhysicsBody.CurrentPosition,
                    alpha);

                if (_config.WorldSimulation is ClientWorldSimulation clientSim)
                {
                    interpPos += clientSim.PositionError;
                }

                _config.PlayerTransform.position =
                    new Vector3(interpPos.x, interpPos.y, interpPos.z);
            }

            // Footstep and fall detection
            _config.FootstepController?.Update();
            _config.FallSoundDetector?.Update();

            // Release finished SFX sources
            _config.SfxSourcePool?.ReleaseFinished();

            profiler.Begin(FrameProfilerSections.Render);
            _config.ChunkMeshStore?.RenderAll(_config.MainCamera);

            if (_config.PlayerRenderer != null)
            {
                _config.PlayerRenderer.Render(
                    _config.PlayerController != null && _config.PlayerController.OnGround,
                    _config.PlayerController != null && _config.PlayerController.IsFlying,
                    _config.BlockInteraction != null && _config.BlockInteraction.IsMining);
            }

            _config.RemotePlayerManager?.RenderAll(_config.MainCamera);

            profiler.End(FrameProfilerSections.Render);
        }

        public void Shutdown()
        {
            _config.ServerLoop?.Shutdown();
            _config.RelightScheduler?.Shutdown();
            _config.MeshScheduler?.Shutdown();
            _config.LODScheduler?.Shutdown();

            _config.PlayerRenderer?.Dispose();
            _config.PlayerRenderer = null;

            _config.RemotePlayerManager?.Dispose();
            _config.RemotePlayerManager = null;
        }

        private int3 GetCameraChunkCoord()
        {
            Vector3 camPos = _config.MainCamera != null
                ? _config.MainCamera.transform.position
                : Vector3.zero;

            return new int3(
                (int)math.floor(camPos.x / ChunkConstants.Size),
                (int)math.floor(camPos.y / ChunkConstants.Size),
                (int)math.floor(camPos.z / ChunkConstants.Size));
        }
    }
}
