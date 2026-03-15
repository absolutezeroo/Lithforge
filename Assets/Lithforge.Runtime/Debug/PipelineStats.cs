using System.Runtime.CompilerServices;
using Lithforge.Voxel.Chunk;

namespace Lithforge.Runtime.Debug
{
    /// <summary>
    /// Static pipeline statistics collector. Counters are incremented by schedulers
    /// and MegaMeshBuffer at the appropriate points. Per-frame counters are reset
    /// at the start of each frame by BeginFrame(). Cumulative counters persist.
    /// All increment methods are AggressiveInlining and gated by Enabled.
    /// </summary>
    public static class PipelineStats
    {
        /// <summary>Gate for all counter operations. When false, increment methods early-out.</summary>
        public static bool Enabled;

        // --- Per-frame counters (reset each frame) ---

        /// <summary>Worldgen jobs dispatched to workers this frame.</summary>
        public static int GenScheduled;

        /// <summary>Worldgen jobs that finished and were polled this frame.</summary>
        public static int GenCompleted;

        /// <summary>LOD0 mesh jobs dispatched to workers this frame.</summary>
        public static int MeshScheduled;

        /// <summary>LOD0 mesh jobs that finished and were polled this frame.</summary>
        public static int MeshCompleted;

        /// <summary>LOD>0 mesh jobs dispatched to workers this frame.</summary>
        public static int LODScheduled;

        /// <summary>LOD>0 mesh jobs that finished and were polled this frame.</summary>
        public static int LODCompleted;

        /// <summary>Decoration passes (tree placement, etc.) executed this frame.</summary>
        public static int DecorateCount;

        /// <summary>Total wall-clock time spent in decoration passes this frame.</summary>
        public static float DecorateMs;

        /// <summary>Bytes uploaded to GPU vertex/index buffers this frame.</summary>
        public static long GpuUploadBytes;

        /// <summary>Number of individual GPU upload operations this frame.</summary>
        public static int GpuUploadCount;

        /// <summary>Slots available in the MegaMeshBuffer free list at snapshot time.</summary>
        public static int FreeListSize;

        /// <summary>MegaMeshBuffer grow (reallocation) events triggered this frame.</summary>
        public static int GrowEvents;

        /// <summary>Chunk mesh invalidations (remesh requests) issued this frame.</summary>
        public static int InvalidateCount;

        /// <summary>GC generation-0 collections that occurred during the previous frame.</summary>
        public static int GcGen0;

        /// <summary>GC generation-1 collections that occurred during the previous frame.</summary>
        public static int GcGen1;

        /// <summary>GC generation-2 (full) collections that occurred during the previous frame.</summary>
        public static int GcGen2;

        /// <summary>Slowest mesh job Complete() call this frame in milliseconds.</summary>
        public static float MeshCompleteMaxMs;

        /// <summary>Mesh Complete() calls that exceeded 1ms, indicating main-thread stalls.</summary>
        public static int MeshCompleteStalls;

        /// <summary>Slowest generation job Complete() call this frame in milliseconds.</summary>
        public static float GenCompleteMaxMs;

        /// <summary>Generation Complete() calls that exceeded 1ms, indicating main-thread stalls.</summary>
        public static int GenCompleteStalls;

        /// <summary>Time spent disposing completed mesh NativeArrays during polling.</summary>
        public static float PollMeshDisposalsMs;

        /// <summary>Time spent uploading mesh data to GPU during polling.</summary>
        public static float PollMeshUploadMs;

        /// <summary>Time spent iterating the in-flight mesh list during polling.</summary>
        public static float PollMeshIterateMs;

        /// <summary>Time spent on the first IsCompleted check during mesh polling.</summary>
        public static float PollMeshFirstIsCompletedMs;

        // --- SchedMesh sub-timings (instrumentation for bottleneck isolation) ---

        /// <summary>Time spent filling the mesh candidate list in MeshScheduler.</summary>
        public static float SchedMeshFillMs;

        /// <summary>Time spent filtering and sorting mesh candidates by priority.</summary>
        public static float SchedMeshFilterMs;

        /// <summary>Time spent allocating NativeArrays for mesh output.</summary>
        public static float SchedMeshAllocMs;

        /// <summary>Time spent calling Schedule() on mesh jobs.</summary>
        public static float SchedMeshScheduleMs;

        /// <summary>Time spent flushing completed mesh data to GPU buffers.</summary>
        public static float SchedMeshFlushMs;

        // --- Previous-frame GC counts for delta computation ---

        private static int s_prevGc0;
        private static int s_prevGc1;
        private static int s_prevGc2;

        // --- Cumulative counters (never reset) ---

        /// <summary>Lifetime count of worldgen jobs completed since launch.</summary>
        public static int TotalGenerated;

        /// <summary>Lifetime count of LOD0 mesh jobs completed since launch.</summary>
        public static int TotalMeshed;

        /// <summary>Lifetime count of LOD>0 mesh jobs completed since launch.</summary>
        public static int TotalLOD;

        /// <summary>Lifetime bytes uploaded to GPU since launch.</summary>
        public static double TotalGpuUploadBytes;

        // --- Chunk state histogram (filled once per frame by UpdateChunkHistogram) ---

        /// <summary>
        /// Chunk count per ChunkState. Indexed by (int)ChunkState.
        /// Length = ChunkState enum value count (8).
        /// </summary>
        public static readonly int[] StateHistogram = new int[8];

        /// <summary>Chunks flagged for remeshing due to block edits or neighbor changes.</summary>
        public static int NeedsRemeshCount;

        /// <summary>Chunks flagged for light recalculation after block edits.</summary>
        public static int NeedsLightUpdateCount;

        // --- Pool stats ---

        /// <summary>NativeArray chunks in the pool ready for reuse.</summary>
        public static int PoolAvailable;

        /// <summary>NativeArray chunks currently checked out to active ManagedChunks.</summary>
        public static int PoolCheckedOut;

        /// <summary>Total NativeArray allocations (available + checked out) in the pool.</summary>
        public static int PoolTotal;

        /// <summary>
        /// Resets all per-frame counters. Called at the start of each frame.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginFrame()
        {
            if (!Enabled)
            {
                return;
            }

            GenScheduled = 0;
            GenCompleted = 0;
            MeshScheduled = 0;
            MeshCompleted = 0;
            LODScheduled = 0;
            LODCompleted = 0;
            DecorateCount = 0;
            DecorateMs = 0f;
            GpuUploadBytes = 0;
            GpuUploadCount = 0;
            FreeListSize = 0;
            GrowEvents = 0;
            InvalidateCount = 0;

            int gc0 = System.GC.CollectionCount(0);
            int gc1 = System.GC.CollectionCount(1);
            int gc2 = System.GC.CollectionCount(2);
            GcGen0 = gc0 - s_prevGc0;
            GcGen1 = gc1 - s_prevGc1;
            GcGen2 = gc2 - s_prevGc2;
            s_prevGc0 = gc0;
            s_prevGc1 = gc1;
            s_prevGc2 = gc2;

            MeshCompleteMaxMs = 0f;
            MeshCompleteStalls = 0;
            GenCompleteMaxMs = 0f;
            GenCompleteStalls = 0;
            PollMeshDisposalsMs = 0f;
            PollMeshUploadMs = 0f;
            PollMeshIterateMs = 0f;
            PollMeshFirstIsCompletedMs = 0f;
            SchedMeshFillMs = 0f;
            SchedMeshFilterMs = 0f;
            SchedMeshAllocMs = 0f;
            SchedMeshScheduleMs = 0f;
            SchedMeshFlushMs = 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IncrGenScheduled()
        {
            if (!Enabled)
            {
                return;
            }

            GenScheduled++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IncrGenCompleted()
        {
            if (!Enabled)
            {
                return;
            }

            GenCompleted++;
            TotalGenerated++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IncrMeshScheduled()
        {
            if (!Enabled)
            {
                return;
            }

            MeshScheduled++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IncrMeshCompleted()
        {
            if (!Enabled)
            {
                return;
            }

            MeshCompleted++;
            TotalMeshed++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IncrLODScheduled()
        {
            if (!Enabled)
            {
                return;
            }

            LODScheduled++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IncrLODCompleted()
        {
            if (!Enabled)
            {
                return;
            }

            LODCompleted++;
            TotalLOD++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddDecorate(float ms)
        {
            if (!Enabled)
            {
                return;
            }

            DecorateCount++;
            DecorateMs += ms;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddGpuUpload(int bytes)
        {
            if (!Enabled)
            {
                return;
            }

            GpuUploadBytes += bytes;
            GpuUploadCount++;
            TotalGpuUploadBytes += bytes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IncrGrow()
        {
            if (!Enabled)
            {
                return;
            }

            GrowEvents++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RecordMeshComplete(float ms)
        {
            if (!Enabled)
            {
                return;
            }

            if (ms > MeshCompleteMaxMs)
            {
                MeshCompleteMaxMs = ms;
            }

            if (ms > 1f)
            {
                MeshCompleteStalls++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RecordGenComplete(float ms)
        {
            if (!Enabled)
            {
                return;
            }

            if (ms > GenCompleteMaxMs)
            {
                GenCompleteMaxMs = ms;
            }

            if (ms > 1f)
            {
                GenCompleteStalls++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IncrInvalidate()
        {
            if (!Enabled)
            {
                return;
            }

            InvalidateCount++;
        }

        /// <summary>
        /// Updates chunk state histogram and pool stats. Called once per frame
        /// from DebugOverlayHUD when benchmark mode is active.
        /// </summary>
        public static void UpdateChunkHistogram(ChunkManager chunkManager, ChunkPool pool)
        {
            if (!Enabled)
            {
                return;
            }

            chunkManager.FillStateHistogram(
                StateHistogram,
                out int needsRemesh,
                out int needsLightUpdate);

            NeedsRemeshCount = needsRemesh;
            NeedsLightUpdateCount = needsLightUpdate;

            PoolAvailable = pool.AvailableCount;
            PoolCheckedOut = pool.CheckedOutCount;
            PoolTotal = pool.TotalAllocated;
        }

        /// <summary>
        /// Total GPU upload in megabytes since launch.
        /// </summary>
        public static float TotalGpuUploadMb
        {
            get { return (float)(TotalGpuUploadBytes / (1024.0 * 1024.0)); }
        }
    }
}
