using Lithforge.Network.Client;
using Lithforge.Network.Server;
using Lithforge.Runtime.Audio;
using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.Debug;
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

namespace Lithforge.Runtime.Session
{
    /// <summary>
    ///     Flat data bag holding all references that <see cref="GameLoopPoco" /> needs.
    ///     Assembled by <see cref="SessionBridgeSubsystem" /> from the initialized subsystems.
    /// </summary>
    public sealed class GameLoopConfig
    {
        // Chunk infrastructure
        public ChunkManager ChunkManager { get; set; }

        public ChunkMeshStore ChunkMeshStore { get; set; }

        public WorldStorage WorldStorage { get; set; }

        public float UnloadBudgetMs { get; set; }

        // Schedulers
        public GenerationScheduler GenerationScheduler { get; set; }

        public RelightScheduler RelightScheduler { get; set; }

        public MeshScheduler MeshScheduler { get; set; }

        public LODScheduler LODScheduler { get; set; }

        public LiquidScheduler LiquidScheduler { get; set; }

        public GpuBufferResizer GpuBufferResizer { get; set; }

        // Culling
        public ChunkCulling Culling { get; set; }

        // Camera
        public Camera MainCamera { get; set; }

        // Simulation
        public IWorldSimulation WorldSimulation { get; set; }

        public InputSnapshotBuilder InputSnapshotBuilder { get; set; }

        public PlayerPhysicsBody PlayerPhysicsBody { get; set; }

        public Transform PlayerTransform { get; set; }

        // Network
        public bool IsClientMode { get; set; }

        public INetworkClient NetworkClient { get; set; }

        public ServerGameLoop ServerGameLoop { get; set; }

        public ClientChunkHandler ClientChunkHandler { get; set; }

        public RemotePlayerManager RemotePlayerManager { get; set; }

        // Player rendering
        public PlayerRenderer PlayerRenderer { get; set; }

        public PlayerController PlayerController { get; set; }

        public BlockInteraction BlockInteraction { get; set; }

        // Gameplay
        public SpawnManager SpawnManager { get; set; }

        public AutoSaveManager AutoSaveManager { get; set; }

        public BlockEntityTickScheduler BlockEntityTickScheduler { get; set; }

        public BiomeTintManager BiomeTintManager { get; set; }

        // Audio
        public FootstepController FootstepController { get; set; }

        public FallSoundDetector FallSoundDetector { get; set; }

        public SfxSourcePool SfxSourcePool { get; set; }

        public AudioEnvironmentController AudioEnvironmentController { get; set; }

        // Debug
        public IFrameProfiler FrameProfiler { get; set; }

        public IPipelineStats PipelineStats { get; set; }

        public MetricsRegistry MetricsRegistry { get; set; }
    }
}
