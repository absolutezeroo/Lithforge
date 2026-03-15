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
        public static bool Enabled;

        // --- Per-frame counters (reset each frame) ---

        public static int GenScheduled;
        public static int GenCompleted;
        public static int MeshScheduled;
        public static int MeshCompleted;
        public static int LODScheduled;
        public static int LODCompleted;
        public static int DecorateCount;
        public static float DecorateMs;
        public static long GpuUploadBytes;
        public static int GpuUploadCount;
        public static int FreeListSize;
        public static int GrowEvents;
        public static int InvalidateCount;
        public static int GcGen0;
        public static int GcGen1;
        public static int GcGen2;
        public static float MeshCompleteMaxMs;
        public static int MeshCompleteStalls;
        public static float GenCompleteMaxMs;
        public static int GenCompleteStalls;
        public static float PollMeshDisposalsMs;
        public static float PollMeshUploadMs;
        public static float PollMeshIterateMs;
        public static float PollMeshFirstIsCompletedMs;

        // --- SchedMesh sub-timings (instrumentation for bottleneck isolation) ---

        public static float SchedMeshFillMs;
        public static float SchedMeshFilterMs;
        public static float SchedMeshAllocMs;
        public static float SchedMeshScheduleMs;
        public static float SchedMeshFlushMs;

        // --- Previous-frame GC counts for delta computation ---

        private static int s_prevGc0;
        private static int s_prevGc1;
        private static int s_prevGc2;

        // --- Cumulative counters (never reset) ---

        public static int TotalGenerated;
        public static int TotalMeshed;
        public static int TotalLOD;
        public static double TotalGpuUploadBytes;

        // --- Chunk state histogram (filled once per frame by UpdateChunkHistogram) ---

        /// <summary>
        /// Chunk count per ChunkState. Indexed by (int)ChunkState.
        /// Length = ChunkState enum value count (8).
        /// </summary>
        public static readonly int[] StateHistogram = new int[8];

        public static int NeedsRemeshCount;
        public static int NeedsLightUpdateCount;

        // --- Pool stats ---

        public static int PoolAvailable;
        public static int PoolCheckedOut;
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
