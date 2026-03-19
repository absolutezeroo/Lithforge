using System;
using System.Runtime.CompilerServices;

namespace Lithforge.Runtime.Debug
{
    /// <summary>
    ///     Pipeline statistics collector implementing IPipelineStats.
    ///     Counters are incremented by schedulers and MegaMeshBuffer.
    ///     Per-frame counters are reset at the start of each frame by BeginFrame().
    ///     Cumulative counters persist. All increment methods are AggressiveInlining
    ///     and gated by Enabled.
    ///     Owner: LithforgeBootstrap. Lifetime: application session.
    /// </summary>
    public sealed class PipelineStats : IPipelineStats
    {
        /// <summary>Previous frame's GC generation-0 collection count for delta calculation.</summary>
        private int _prevGc0;

        /// <summary>Previous frame's GC generation-1 collection count for delta calculation.</summary>
        private int _prevGc1;

        /// <summary>Previous frame's GC generation-2 collection count for delta calculation.</summary>
        private int _prevGc2;

        /// <summary>Enables or disables statistics collection. When false, all increment methods are no-ops.</summary>
        public bool Enabled { get; set; }

        /// <summary>Number of generation jobs scheduled this frame.</summary>
        public int GenScheduled { get; private set; }

        /// <summary>Number of generation jobs completed this frame.</summary>
        public int GenCompleted { get; private set; }

        /// <summary>Number of LOD0 mesh jobs scheduled this frame.</summary>
        public int MeshScheduled { get; private set; }

        /// <summary>Number of LOD0 mesh jobs completed this frame.</summary>
        public int MeshCompleted { get; private set; }

        /// <summary>Number of LOD>0 mesh jobs scheduled this frame.</summary>
        public int LODScheduled { get; private set; }

        /// <summary>Number of LOD>0 mesh jobs completed this frame.</summary>
        public int LODCompleted { get; private set; }

        /// <summary>Number of decoration passes executed this frame.</summary>
        public int DecorateCount { get; private set; }

        /// <summary>Total milliseconds spent in decoration passes this frame.</summary>
        public float DecorateMs { get; private set; }

        /// <summary>Total bytes uploaded to GPU this frame.</summary>
        public long GpuUploadBytes { get; private set; }

        /// <summary>Number of GPU upload operations this frame.</summary>
        public int GpuUploadCount { get; private set; }

        /// <summary>Number of MegaMeshBuffer grow (reallocation) events this frame.</summary>
        public int GrowEvents { get; private set; }

        /// <summary>Number of chunk mesh invalidations this frame.</summary>
        public int InvalidateCount { get; private set; }

        /// <summary>Delta GC generation-0 collections since last frame.</summary>
        public int GcGen0 { get; private set; }

        /// <summary>Delta GC generation-1 collections since last frame.</summary>
        public int GcGen1 { get; private set; }

        /// <summary>Delta GC generation-2 (full) collections since last frame.</summary>
        public int GcGen2 { get; private set; }

        /// <summary>Slowest mesh Complete() call in milliseconds this frame.</summary>
        public float MeshCompleteMaxMs { get; private set; }

        /// <summary>Number of mesh Complete() calls that exceeded 1ms this frame.</summary>
        public int MeshCompleteStalls { get; private set; }

        /// <summary>Slowest generation Complete() call in milliseconds this frame.</summary>
        public float GenCompleteMaxMs { get; private set; }

        /// <summary>Number of generation Complete() calls that exceeded 1ms this frame.</summary>
        public int GenCompleteStalls { get; private set; }

        /// <summary>Time spent disposing completed mesh NativeContainers in milliseconds.</summary>
        public float PollMeshDisposalsMs { get; set; }

        /// <summary>Time spent uploading mesh data to GPU in milliseconds.</summary>
        public float PollMeshUploadMs { get; set; }

        /// <summary>Time spent iterating over in-flight mesh jobs in milliseconds.</summary>
        public float PollMeshIterateMs { get; set; }

        /// <summary>Time spent on the first IsCompleted check for mesh jobs in milliseconds.</summary>
        public float PollMeshFirstIsCompletedMs { get; set; }

        /// <summary>Time spent filling the mesh candidate list in milliseconds.</summary>
        public float SchedMeshFillMs { get; set; }

        /// <summary>Time spent filtering and sorting mesh candidates in milliseconds.</summary>
        public float SchedMeshFilterMs { get; set; }

        /// <summary>Time spent allocating NativeArrays for mesh output in milliseconds.</summary>
        public float SchedMeshAllocMs { get; set; }

        /// <summary>Time spent calling Schedule() on mesh jobs in milliseconds.</summary>
        public float SchedMeshScheduleMs { get; set; }

        /// <summary>Time spent flushing completed mesh data to GPU buffers in milliseconds.</summary>
        public float SchedMeshFlushMs { get; set; }

        /// <summary>Cumulative total of generation jobs completed across the session.</summary>
        public int TotalGenerated { get; private set; }

        /// <summary>Cumulative total of LOD0 mesh jobs completed across the session.</summary>
        public int TotalMeshed { get; private set; }

        /// <summary>Cumulative total of LOD>0 mesh jobs completed across the session.</summary>
        public int TotalLOD { get; private set; }

        /// <summary>Cumulative total bytes uploaded to GPU across the session.</summary>
        public double TotalGpuUploadBytes { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginFrame()
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
            GrowEvents = 0;
            InvalidateCount = 0;

            int gc0 = GC.CollectionCount(0);
            int gc1 = GC.CollectionCount(1);
            int gc2 = GC.CollectionCount(2);
            GcGen0 = gc0 - _prevGc0;
            GcGen1 = gc1 - _prevGc1;
            GcGen2 = gc2 - _prevGc2;
            _prevGc0 = gc0;
            _prevGc1 = gc1;
            _prevGc2 = gc2;

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
        public void IncrGenScheduled()
        {
            if (!Enabled)
            {
                return;
            }

            GenScheduled++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrGenCompleted()
        {
            if (!Enabled)
            {
                return;
            }

            GenCompleted++;
            TotalGenerated++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrMeshScheduled()
        {
            if (!Enabled)
            {
                return;
            }

            MeshScheduled++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrMeshCompleted()
        {
            if (!Enabled)
            {
                return;
            }

            MeshCompleted++;
            TotalMeshed++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrLODScheduled()
        {
            if (!Enabled)
            {
                return;
            }

            LODScheduled++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrLODCompleted()
        {
            if (!Enabled)
            {
                return;
            }

            LODCompleted++;
            TotalLOD++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrGrow()
        {
            if (!Enabled)
            {
                return;
            }

            GrowEvents++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrInvalidate()
        {
            if (!Enabled)
            {
                return;
            }

            InvalidateCount++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddDecorate(float ms)
        {
            if (!Enabled)
            {
                return;
            }

            DecorateCount++;
            DecorateMs += ms;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddGpuUpload(int bytes)
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
        public void RecordMeshComplete(float ms)
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
        public void RecordGenComplete(float ms)
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
    }
}
