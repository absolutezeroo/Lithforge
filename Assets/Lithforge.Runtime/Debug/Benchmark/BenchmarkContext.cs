using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Input;
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
        public MetricsRegistry Metrics { get; set; }
        public ChunkManager ChunkManager { get; set; }
        public PlayerController PlayerController { get; set; }
        public Transform PlayerTransform { get; set; }
        public Camera MainCamera { get; set; }
        public GameLoop GameLoop { get; set; }
        public BlockInteraction BlockInteraction { get; set; }

        /// <summary>
        /// Returns true when all generation and meshing queues have drained.
        /// </summary>
        public bool IsPipelineQuiescent
        {
            get
            {
                if (GameLoop == null)
                {
                    return true;
                }

                return GameLoop.PendingGenerationCount == 0
                    && GameLoop.PendingMeshCount == 0
                    && GameLoop.PendingLODMeshCount == 0;
            }
        }
    }
}
