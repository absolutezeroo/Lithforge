using System;
using System.Collections.Generic;

using Lithforge.Network.Bridge;
using Lithforge.Network.Client;
using Lithforge.Network.Server;
using Lithforge.Runtime.Audio;
using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Debug.Benchmark;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Network;
using Lithforge.Runtime.Player;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;

using Unity.Mathematics;

using UnityEngine;

using Object = UnityEngine.Object;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>
    ///     Phase 11 — capstone subsystem that assembles the <see cref="GameLoopConfig" />,
    ///     creates the <see cref="GameLoopPoco" />, and activates the <see cref="SessionBridge" />
    ///     MonoBehaviour to start pumping Update/LateUpdate.
    /// </summary>
    public sealed class SessionBridgeSubsystem : IGameSubsystem
    {
        /// <summary>The owned bridge GameObject hosting the SessionBridge MonoBehaviour.</summary>
        private GameObject _bridgeObject;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get { return "SessionBridge"; }
        }

        /// <summary>Depends on all other subsystems; must be the last to initialize.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(AudioSubsystem),
            typeof(MetricsSubsystem),
            typeof(PauseMenuSubsystem),
            typeof(BenchmarkSubsystem),
            typeof(RemotePlayerManagerSubsystem),
        };

        /// <summary>Always created for all session types.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return true;
        }

        /// <summary>Creates the bridge GameObject; activation deferred to PostInitialize.</summary>
        public void Initialize(SessionContext context)
        {
            // Create the bridge GameObject; activation happens in PostInitialize
            // after all subsystems have registered their services.
            _bridgeObject = new GameObject("SessionBridge");
        }

        /// <summary>Assembles the GameLoopConfig, creates GameLoopPoco, and activates the bridge.</summary>
        public void PostInitialize(SessionContext context)
        {
            GameLoopConfig config = new();

            // Chunk infrastructure
            if (context.TryGet(out ChunkManager chunkManager))
            {
                config.ChunkManager = chunkManager;
            }

            if (context.TryGet(out ChunkMeshStore chunkMeshStore))
            {
                config.ChunkMeshStore = chunkMeshStore;
            }

            // Schedulers (rendering-side only)
            if (context.TryGet(out RelightScheduler relightScheduler))
            {
                config.RelightScheduler = relightScheduler;
            }

            if (context.TryGet(out MeshScheduler meshScheduler))
            {
                config.MeshScheduler = meshScheduler;
            }

            if (context.TryGet(out LODScheduler lodScheduler))
            {
                config.LODScheduler = lodScheduler;
            }

            if (context.TryGet(out LiquidScheduler liquidScheduler))
            {
                config.LiquidScheduler = liquidScheduler;
            }

            if (context.TryGet(out GpuBufferResizer gpuBufferResizer))
            {
                config.GpuBufferResizer = gpuBufferResizer;
            }

            // Culling
            if (context.TryGet(out ChunkCulling culling))
            {
                config.Culling = culling;
            }

            // Camera + player
            if (context.TryGet(out PlayerTransformHolder player))
            {
                config.MainCamera = player.MainCamera;
                config.PlayerTransform = player.Transform;
                config.PlayerPhysicsBody = player.PhysicsBody;
                config.PlayerRenderer = player.Renderer;
                config.PlayerController = player.Controller;
            }

            // Simulation
            if (context.TryGet(out IWorldSimulation worldSimulation))
            {
                config.WorldSimulation = worldSimulation;
            }

            if (context.TryGet(out InputSnapshotBuilder inputSnapshotBuilder))
            {
                config.InputSnapshotBuilder = inputSnapshotBuilder;
            }

            // Network
            if (context.TryGet(out INetworkClient networkClient))
            {
                config.NetworkClient = networkClient;
            }

            if (context.TryGet(out ServerGameLoop serverGameLoop))
            {
                config.ServerGameLoop = serverGameLoop;
            }

            if (context.TryGet(out MainThreadBridgePump transportPump))
            {
                config.TransportPump = transportPump;
            }

            if (context.TryGet(out ServerThreadRunner serverThreadRunner))
            {
                config.ServerThreadRunner = serverThreadRunner;
            }

            if (context.TryGet(out ClientChunkHandler clientChunkHandler))
            {
                config.ClientChunkHandler = clientChunkHandler;
            }

            if (context.TryGet(out RemotePlayerManager remotePlayerManager))
            {
                config.RemotePlayerManager = remotePlayerManager;
            }

            // Block interaction
            if (context.TryGet(out BlockInteraction blockInteraction))
            {
                config.BlockInteraction = blockInteraction;
            }

            // Gameplay
            if (context.TryGet(out BlockEntityTickScheduler blockEntityTickScheduler))
            {
                config.BlockEntityTickScheduler = blockEntityTickScheduler;
            }

            if (context.TryGet(out BiomeTintManager biomeTintManager))
            {
                config.BiomeTintManager = biomeTintManager;
            }

            // Audio
            if (context.TryGet(out FootstepController footstepController))
            {
                config.FootstepController = footstepController;
            }

            if (context.TryGet(out FallSoundDetector fallSoundDetector))
            {
                config.FallSoundDetector = fallSoundDetector;
            }

            if (context.TryGet(out SfxSourcePool sfxSourcePool))
            {
                config.SfxSourcePool = sfxSourcePool;
            }

            if (context.TryGet(out AudioEnvironmentController audioEnvController))
            {
                config.AudioEnvironmentController = audioEnvController;
            }

            // Debug
            config.FrameProfiler = context.App.FrameProfiler;
            config.PipelineStats = context.App.PipelineStats;

            if (context.TryGet(out MetricsRegistry metricsRegistry))
            {
                config.MetricsRegistry = metricsRegistry;
            }

            // Build ServerLoopPoco for SP/Host modes
            if (context.Config is SessionConfig.Singleplayer or SessionConfig.Host)
            {
                config.ServerLoop = BuildServerLoop(context, player);
            }

            // Create and activate the game loop
            GameLoopPoco gameLoop = new(config);
            SessionBridge bridge = _bridgeObject.AddComponent<SessionBridge>();
            bridge.Activate(gameLoop);

            context.Register(bridge);
            context.Register(gameLoop);

            // Wire late references that depend on the GameLoopPoco
            if (context.TryGet(out MetricsRegistry metrics))
            {
                metrics.SetGameLoopPoco(gameLoop);
            }

            if (context.TryGet(out BenchmarkRunner benchmarkRunner))
            {
                benchmarkRunner.SetGameLoopPoco(gameLoop);
            }
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>Deactivates the bridge and destroys the bridge GameObject.</summary>
        public void Dispose()
        {
            if (_bridgeObject != null)
            {
                SessionBridge bridge = _bridgeObject.GetComponent<SessionBridge>();

                bridge?.Deactivate();
                Object.Destroy(_bridgeObject);

                _bridgeObject = null;
            }
        }

        /// <summary>Builds the server-side loop POCO from registered subsystem services.</summary>
        private static ServerLoopPoco BuildServerLoop(
            SessionContext context,
            PlayerTransformHolder player)
        {
            ServerLoopConfig slConfig = new();

            if (context.TryGet(out ChunkManager chunkManager))
            {
                slConfig.ChunkManager = chunkManager;
            }

            if (context.TryGet(out WorldStorage worldStorage))
            {
                slConfig.WorldStorage = worldStorage;
            }

            if (context.TryGet(out GenerationScheduler generationScheduler))
            {
                slConfig.GenerationScheduler = generationScheduler;
            }

            if (context.TryGet(out RelightScheduler relightScheduler))
            {
                slConfig.RelightScheduler = relightScheduler;
            }

            if (context.TryGet(out LiquidScheduler liquidScheduler))
            {
                slConfig.LiquidScheduler = liquidScheduler;
            }

            if (context.TryGet(out AutoSaveManager autoSaveManager))
            {
                slConfig.AutoSaveManager = autoSaveManager;
            }

            if (context.TryGet(out BlockEntityTickScheduler blockEntityTickScheduler))
            {
                slConfig.BlockEntityTickScheduler = blockEntityTickScheduler;
            }

            slConfig.UnloadBudgetMs = context.App.Settings.Chunk.UnloadBudgetMs;
            slConfig.GracePeriodSeconds = context.App.Settings.Chunk.GracePeriodSeconds;
            slConfig.GetCurrentRealtime = () => Time.realtimeSinceStartupAsDouble;

            // Camera drives chunk loading for the local player (always correct — not
            // affected by server-side physics falling through unloaded chunks).
            // Bridge provides chunk coords for remote players only.
            Camera mainCamera = player?.MainCamera;

            context.TryGet(out MainThreadBridgePump pump);

            slConfig.GetPlayerChunkSnapshot = () =>
            {
                int3 cameraCoord = default;
                bool hasCamera = false;

                if (mainCamera is not null)
                {
                    Vector3 pos = mainCamera.transform.position;

                    cameraCoord = new int3(
                        (int)math.floor(pos.x / ChunkConstants.Size),
                        (int)math.floor(pos.y / ChunkConstants.Size),
                        (int)math.floor(pos.z / ChunkConstants.Size));

                    hasCamera = true;
                }

                // Bridge adds remote players (local peer's stale server-side position
                // is harmless noise — the camera coord is authoritative for loading).
                PlayerChunkSnapshot bridge = pump is not null
                    ? pump.GetPlayerChunkSnapshot()
                    : PlayerChunkSnapshot.Empty;

                int total = (hasCamera ? 1 : 0) + bridge.Count;

                if (total == 0)
                {
                    return PlayerChunkSnapshot.Empty;
                }

                int3[] coords = new int3[total];
                int idx = 0;

                if (hasCamera)
                {
                    coords[idx++] = cameraCoord;
                }

                for (int i = 0; i < bridge.Count; i++)
                {
                    coords[idx++] = bridge.Coords[i];
                }

                return new PlayerChunkSnapshot(coords, total);
            };

            Camera lookCamera = player?.MainCamera;

            slConfig.GetServerLookAhead = () =>
            {
                if (lookCamera == null)
                {
                    return float3.zero;
                }

                Vector3 fwd = lookCamera.transform.forward;

                return new float3(fwd.x, fwd.y, fwd.z);
            };

            return new ServerLoopPoco(slConfig);
        }
    }
}
