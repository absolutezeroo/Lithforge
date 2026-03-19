namespace Lithforge.Runtime.Debug
{
    /// <summary>
    ///     Per-session pipeline statistics collector. Counters are incremented by
    ///     schedulers and MegaMeshBuffer at the appropriate points. Per-frame counters
    ///     are reset at the start of each frame by BeginFrame().
    ///     Owner: LithforgeBootstrap. Injected into GameLoop, schedulers, MegaMeshBuffer,
    ///     MetricsRegistry, F3DebugOverlay, and BenchmarkRunner.
    /// </summary>
    public interface IPipelineStats
    {
        /// <summary>Enables or disables statistics collection. When false, all increment methods are no-ops.</summary>
        public bool Enabled { get; set; }

        /// <summary>Number of generation jobs scheduled this frame.</summary>
        public int GenScheduled { get; }

        /// <summary>Number of generation jobs completed this frame.</summary>
        public int GenCompleted { get; }

        /// <summary>Number of LOD0 mesh jobs scheduled this frame.</summary>
        public int MeshScheduled { get; }

        /// <summary>Number of LOD0 mesh jobs completed this frame.</summary>
        public int MeshCompleted { get; }

        /// <summary>Number of LOD>0 mesh jobs scheduled this frame.</summary>
        public int LODScheduled { get; }

        /// <summary>Number of LOD>0 mesh jobs completed this frame.</summary>
        public int LODCompleted { get; }

        /// <summary>Number of decoration passes executed this frame.</summary>
        public int DecorateCount { get; }

        /// <summary>Total milliseconds spent in decoration passes this frame.</summary>
        public float DecorateMs { get; }

        /// <summary>Total bytes uploaded to GPU this frame.</summary>
        public long GpuUploadBytes { get; }

        /// <summary>Number of GPU upload operations this frame.</summary>
        public int GpuUploadCount { get; }

        /// <summary>Number of MegaMeshBuffer grow (reallocation) events this frame.</summary>
        public int GrowEvents { get; }

        /// <summary>Number of chunk mesh invalidations this frame.</summary>
        public int InvalidateCount { get; }

        /// <summary>Delta GC generation-0 collections since last frame.</summary>
        public int GcGen0 { get; }

        /// <summary>Delta GC generation-1 collections since last frame.</summary>
        public int GcGen1 { get; }

        /// <summary>Delta GC generation-2 (full) collections since last frame.</summary>
        public int GcGen2 { get; }

        /// <summary>Slowest mesh Complete() call in milliseconds this frame.</summary>
        public float MeshCompleteMaxMs { get; }

        /// <summary>Number of mesh Complete() calls that exceeded 1ms this frame.</summary>
        public int MeshCompleteStalls { get; }

        /// <summary>Slowest generation Complete() call in milliseconds this frame.</summary>
        public float GenCompleteMaxMs { get; }

        /// <summary>Number of generation Complete() calls that exceeded 1ms this frame.</summary>
        public int GenCompleteStalls { get; }

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
        public int TotalGenerated { get; }

        /// <summary>Cumulative total of LOD0 mesh jobs completed across the session.</summary>
        public int TotalMeshed { get; }

        /// <summary>Cumulative total of LOD>0 mesh jobs completed across the session.</summary>
        public int TotalLOD { get; }

        /// <summary>Cumulative total bytes uploaded to GPU across the session.</summary>
        public double TotalGpuUploadBytes { get; }

        /// <summary>Resets all per-frame counters and captures GC delta counts.</summary>
        public void BeginFrame();

        /// <summary>Increments the per-frame generation-scheduled counter.</summary>
        public void IncrGenScheduled();

        /// <summary>Increments the per-frame generation-completed counter and cumulative total.</summary>
        public void IncrGenCompleted();

        /// <summary>Increments the per-frame mesh-scheduled counter.</summary>
        public void IncrMeshScheduled();

        /// <summary>Increments the per-frame mesh-completed counter and cumulative total.</summary>
        public void IncrMeshCompleted();

        /// <summary>Increments the per-frame LOD-scheduled counter.</summary>
        public void IncrLODScheduled();

        /// <summary>Increments the per-frame LOD-completed counter and cumulative total.</summary>
        public void IncrLODCompleted();

        /// <summary>Increments the per-frame MegaMeshBuffer grow event counter.</summary>
        public void IncrGrow();

        /// <summary>Increments the per-frame chunk mesh invalidation counter.</summary>
        public void IncrInvalidate();

        /// <summary>Records a decoration pass with the specified duration in milliseconds.</summary>
        public void AddDecorate(float ms);

        /// <summary>Records a GPU upload of the specified size in bytes.</summary>
        public void AddGpuUpload(int bytes);

        /// <summary>Records a mesh Complete() call duration and tracks stalls exceeding 1ms.</summary>
        public void RecordMeshComplete(float ms);

        /// <summary>Records a generation Complete() call duration and tracks stalls exceeding 1ms.</summary>
        public void RecordGenComplete(float ms);
    }
}
