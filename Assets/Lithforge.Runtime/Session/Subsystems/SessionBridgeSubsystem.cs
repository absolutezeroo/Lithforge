using System;
using System.Collections.Generic;

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
using Lithforge.Runtime.Spawn;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;

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
        private GameObject _bridgeObject;

        public string Name
        {
            get
            {
                return "SessionBridge";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(AudioSubsystem),
            typeof(MetricsSubsystem),
            typeof(PauseMenuSubsystem),
            typeof(BenchmarkSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return true;
        }

        public void Initialize(SessionContext context)
        {
            // Create the bridge GameObject; activation happens in PostInitialize
            // after all subsystems have registered their services.
            _bridgeObject = new GameObject("SessionBridge");
        }

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

            if (context.TryGet(out WorldStorage worldStorage))
            {
                config.WorldStorage = worldStorage;
            }

            config.UnloadBudgetMs = context.App.Settings.Chunk.UnloadBudgetMs;

            // Schedulers
            if (context.TryGet(out GenerationScheduler generationScheduler))
            {
                config.GenerationScheduler = generationScheduler;
            }

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
            config.IsClientMode = context.Config is SessionConfig.Client;

            if (context.TryGet(out INetworkClient networkClient))
            {
                config.NetworkClient = networkClient;
            }

            if (context.TryGet(out ServerGameLoop serverGameLoop))
            {
                config.ServerGameLoop = serverGameLoop;
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
            if (context.TryGet(out SpawnManager spawnManager))
            {
                config.SpawnManager = spawnManager;
            }

            if (context.TryGet(out AutoSaveManager autoSaveManager))
            {
                config.AutoSaveManager = autoSaveManager;
            }

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

        public void Shutdown()
        {
        }

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
    }
}
