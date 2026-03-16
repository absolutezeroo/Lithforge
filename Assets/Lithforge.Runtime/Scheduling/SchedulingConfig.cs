using Unity.Mathematics;

namespace Lithforge.Runtime.Scheduling
{
    /// <summary>
    /// Derives all scheduling parameters from renderDistance.
    /// Single source of truth — no hardcoded constants elsewhere.
    /// Generation and mesh scheduling intentionally use the same throughput formula
    /// so both pipelines advance at the same rate, avoiding one starving the other.
    /// </summary>
    public static class SchedulingConfig
    {
        /// <summary>
        /// How many generation jobs to schedule per frame. Scales linearly with render
        /// distance, capped at 16 to avoid overwhelming the job system.
        /// </summary>
        /// <param name="rd">Current render distance in chunks.</param>
        public static int MaxGenerationsPerFrame(int rd)
        {
            return math.min(16, 4 + rd / 2);
        }

        /// <summary>
        /// How many mesh jobs to schedule per frame. Mirrors MaxGenerationsPerFrame
        /// so neither pipeline starves the other.
        /// </summary>
        /// <param name="rd">Current render distance in chunks.</param>
        public static int MaxMeshesPerFrame(int rd)
        {
            return math.min(16, 4 + rd / 2);
        }

        /// <summary>
        /// How many generation job completions to poll per frame. Capped at 6 to keep
        /// PollGen under ~1.5ms — each completion runs decoration on the main thread
        /// (~0.22ms), so 6 completions ~ 1.3ms vs 10 completions ~ 2.2ms.
        /// </summary>
        /// <param name="rd">Current render distance in chunks.</param>
        public static int MaxGenCompletionsPerFrame(int rd)
        {
            return math.min(6, 3 + rd / 4);
        }

        /// <summary>
        /// How many mesh job completions (including GPU upload) to process per frame.
        /// </summary>
        /// <param name="rd">Current render distance in chunks.</param>
        public static int MaxMeshCompletionsPerFrame(int rd)
        {
            return math.min(32, 4 + rd / 2);
        }

        /// <summary>
        /// How many LOD mesh jobs (downsample + greedy mesh) to schedule per frame.
        /// Lower budget than LOD0 because LOD meshes are larger and less urgent.
        /// </summary>
        /// <param name="rd">Current render distance in chunks.</param>
        public static int MaxLODMeshesPerFrame(int rd)
        {
            return math.min(8, 2 + rd / 4);
        }

        /// <summary>
        /// How many LOD mesh completions to process per frame. Kept low to avoid
        /// GPU upload floods — LOD meshes are ~35KB each, so 4 completions ≈ 140KB/frame.
        /// </summary>
        /// <param name="rd">Current render distance in chunks.</param>
        public static int MaxLODCompletionsPerFrame(int rd)
        {
            return math.min(4, 2 + rd / 6);
        }

        /// <summary>
        /// Chebyshev XZ distance (in chunks) beyond which LOD1 (2x2x2 merge) begins.
        /// Minimum 5 ensures ~28-38% of chunks are LOD0 at typical render distances,
        /// preventing the mesh pipeline from being starved by the LODLevel &gt; 0 filter.
        /// </summary>
        /// <param name="rd">Current render distance in chunks.</param>
        public static int LOD1Distance(int rd)
        {
            return math.max(5, rd * 2 / 3);
        }

        /// <summary>
        /// Chebyshev XZ distance beyond which LOD2 (4x4x4 merge) begins.
        /// </summary>
        /// <param name="rd">Current render distance in chunks.</param>
        public static int LOD2Distance(int rd)
        {
            return math.max(7, rd * 4 / 5);
        }

        /// <summary>
        /// Chebyshev XZ distance beyond which LOD3 (8x8x8 merge) begins.
        /// </summary>
        /// <param name="rd">Current render distance in chunks.</param>
        public static int LOD3Distance(int rd)
        {
            return math.max(9, rd - 1);
        }

        /// <summary>
        /// Number of in-flight mesh jobs before the scheduler starts ramping down
        /// new schedules. Prevents the main thread from being starved by too many
        /// pending completion polls and GPU uploads.
        /// </summary>
        /// <param name="rd">Current render distance in chunks.</param>
        public static int ThrottleThreshold(int rd)
        {
            return math.clamp(rd * 2, 16, 64);
        }
    }
}
