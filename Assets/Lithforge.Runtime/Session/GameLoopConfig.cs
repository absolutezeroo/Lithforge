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
using Lithforge.Runtime.Tick;
using Lithforge.Voxel.Chunk;

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

        // Schedulers (rendering-side only — generation scheduling moved to ServerLoopPoco)
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
        public INetworkClient NetworkClient { get; set; }

        public ServerGameLoop ServerGameLoop { get; set; }

        public ClientChunkHandler ClientChunkHandler { get; set; }

        public RemotePlayerManager RemotePlayerManager { get; set; }

        // Server-side loop
        public ServerLoopPoco ServerLoop { get; set; }

        // Player rendering
        public PlayerRenderer PlayerRenderer { get; set; }

        public PlayerController PlayerController { get; set; }

        public BlockInteraction BlockInteraction { get; set; }

        // Gameplay
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
