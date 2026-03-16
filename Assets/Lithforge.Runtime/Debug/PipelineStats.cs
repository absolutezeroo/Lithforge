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
        private int _prevGc0;

        private int _prevGc1;

        private int _prevGc2;

        public bool Enabled { get; set; }

        public int GenScheduled { get; private set; }

        public int GenCompleted { get; private set; }

        public int MeshScheduled { get; private set; }

        public int MeshCompleted { get; private set; }

        public int LODScheduled { get; private set; }

        public int LODCompleted { get; private set; }

        public int DecorateCount { get; private set; }

        public float DecorateMs { get; private set; }

        public long GpuUploadBytes { get; private set; }

        public int GpuUploadCount { get; private set; }

        public int GrowEvents { get; private set; }

        public int InvalidateCount { get; private set; }

        public int GcGen0 { get; private set; }

        public int GcGen1 { get; private set; }

        public int GcGen2 { get; private set; }

        public float MeshCompleteMaxMs { get; private set; }

        public int MeshCompleteStalls { get; private set; }

        public float GenCompleteMaxMs { get; private set; }

        public int GenCompleteStalls { get; private set; }

        public float PollMeshDisposalsMs { get; set; }

        public float PollMeshUploadMs { get; set; }

        public float PollMeshIterateMs { get; set; }

        public float PollMeshFirstIsCompletedMs { get; set; }

        public float SchedMeshFillMs { get; set; }

        public float SchedMeshFilterMs { get; set; }

        public float SchedMeshAllocMs { get; set; }

        public float SchedMeshScheduleMs { get; set; }

        public float SchedMeshFlushMs { get; set; }

        public int TotalGenerated { get; private set; }

        public int TotalMeshed { get; private set; }

        public int TotalLOD { get; private set; }

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
