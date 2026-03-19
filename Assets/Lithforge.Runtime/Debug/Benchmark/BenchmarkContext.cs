using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Session;
using Lithforge.Voxel.Chunk;
using UnityEngine;

namespace Lithforge.Runtime.Debug.Benchmark
{
    /// <summary>
    /// Provides benchmark commands with access to engine systems.
    /// Created once per benchmark session and passed to every command's Execute method.
    /// </summary>
    public sealed class BenchmarkContext
    {
        /// <summary>Shared metrics registry for recording per-frame performance data.</summary>
        public MetricsRegistry Metrics { get; set; }

        /// <summary>Chunk manager for querying world state during benchmarks.</summary>
        public ChunkManager ChunkManager { get; set; }

        /// <summary>Player controller for movement and physics during fly-through benchmarks.</summary>
        public PlayerController PlayerController { get; set; }

        /// <summary>Player transform for direct position and rotation manipulation.</summary>
        public Transform PlayerTransform { get; set; }

        /// <summary>Main camera for frustum and rendering queries.</summary>
        public Camera MainCamera { get; set; }

        /// <summary>Game loop reference for querying pipeline queue depths and spawn readiness.</summary>
        public GameLoopPoco GameLoopPoco { get; set; }

        /// <summary>Block interaction system for placing and breaking blocks during benchmarks.</summary>
        public BlockInteraction BlockInteraction { get; set; }

        /// <summary>
        /// Returns true when all generation and meshing queues have drained.
        /// </summary>
        public bool IsPipelineQuiescent
        {
            get
            {
                if (GameLoopPoco == null)
                {
                    return true;
                }

                return GameLoopPoco.PendingGenerationCount == 0
                    && GameLoopPoco.PendingMeshCount == 0
                    && GameLoopPoco.PendingLODMeshCount == 0;
            }
        }
    }
}
