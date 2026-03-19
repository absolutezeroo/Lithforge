using Lithforge.Network.Bridge;
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
        /// <summary>Manages chunk lifecycle, loading, and unloading.</summary>
        public ChunkManager ChunkManager { get; set; }

        /// <summary>GPU-driven chunk mesh storage and indirect draw manager.</summary>
        public ChunkMeshStore ChunkMeshStore { get; set; }

        /// <summary>Schedules cross-chunk light update jobs after block edits.</summary>
        public RelightScheduler RelightScheduler { get; set; }

        /// <summary>Schedules Burst greedy mesh jobs for LOD0 chunks.</summary>
        public MeshScheduler MeshScheduler { get; set; }

        /// <summary>Schedules downsample and mesh jobs for LOD1+ chunks.</summary>
        public LODScheduler LODScheduler { get; set; }

        /// <summary>Schedules liquid flow simulation jobs.</summary>
        public LiquidScheduler LiquidScheduler { get; set; }

        /// <summary>Handles dynamic GPU buffer resizing when capacity is exceeded.</summary>
        public GpuBufferResizer GpuBufferResizer { get; set; }

        /// <summary>Frustum and occlusion culling for visible chunk determination.</summary>
        public ChunkCulling Culling { get; set; }

        /// <summary>The main camera used for rendering and frustum culling.</summary>
        public Camera MainCamera { get; set; }

        /// <summary>The world simulation that drives fixed-tick updates.</summary>
        public IWorldSimulation WorldSimulation { get; set; }

        /// <summary>Builds input snapshots from per-frame input for fixed-tick consumption.</summary>
        public InputSnapshotBuilder InputSnapshotBuilder { get; set; }

        /// <summary>Player physics body running at fixed tick rate.</summary>
        public PlayerPhysicsBody PlayerPhysicsBody { get; set; }

        /// <summary>Transform of the local player for position interpolation.</summary>
        public Transform PlayerTransform { get; set; }

        /// <summary>Network client for sending commands and receiving state updates.</summary>
        public INetworkClient NetworkClient { get; set; }

        /// <summary>Server-side game loop for host/singleplayer network processing (unused when threaded).</summary>
        public ServerGameLoop ServerGameLoop { get; set; }

        /// <summary>Main-thread bridge pump for servicing the background server thread.</summary>
        public MainThreadBridgePump TransportPump { get; set; }

        /// <summary>Background server thread runner for fault checking and shutdown.</summary>
        public ServerThreadRunner ServerThreadRunner { get; set; }

        /// <summary>Handles chunk data received from the network for client-side loading.</summary>
        public ClientChunkHandler ClientChunkHandler { get; set; }

        /// <summary>Manages remote player entities for rendering and interpolation.</summary>
        public RemotePlayerManager RemotePlayerManager { get; set; }

        /// <summary>Server-side loop handling generation, loading, and gameplay ticking.</summary>
        public ServerLoopPoco ServerLoop { get; set; }

        /// <summary>Renders the local player model (first-person arm, third-person body).</summary>
        public PlayerRenderer PlayerRenderer { get; set; }

        /// <summary>Player input controller (thin passthrough to physics body).</summary>
        public PlayerController PlayerController { get; set; }

        /// <summary>Block mining and placement interaction handler.</summary>
        public BlockInteraction BlockInteraction { get; set; }

        /// <summary>Round-robin tick scheduler for block entity behaviors.</summary>
        public BlockEntityTickScheduler BlockEntityTickScheduler { get; set; }

        /// <summary>Applies biome-dependent tint colors to chunk meshes.</summary>
        public BiomeTintManager BiomeTintManager { get; set; }

        /// <summary>Plays distance-based footstep sounds for the local player.</summary>
        public FootstepController FootstepController { get; set; }

        /// <summary>Detects falls and plays landing impact sounds.</summary>
        public FallSoundDetector FallSoundDetector { get; set; }

        /// <summary>Pool of reusable AudioSource instances for one-shot sound effects.</summary>
        public SfxSourcePool SfxSourcePool { get; set; }

        /// <summary>Frame-rate audio environment updates (filters, reverb, crossfades).</summary>
        public AudioEnvironmentController AudioEnvironmentController { get; set; }

        /// <summary>Zero-alloc frame section profiler for timing measurements.</summary>
        public IFrameProfiler FrameProfiler { get; set; }

        /// <summary>Pipeline counters tracking per-frame and cumulative statistics.</summary>
        public IPipelineStats PipelineStats { get; set; }

        /// <summary>Shared metrics data source for overlay and benchmarks.</summary>
        public MetricsRegistry MetricsRegistry { get; set; }
    }
}
