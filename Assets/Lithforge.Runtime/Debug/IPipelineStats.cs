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
        public bool Enabled { get; set; }

        public int GenScheduled { get; }

        public int GenCompleted { get; }

        public int MeshScheduled { get; }

        public int MeshCompleted { get; }

        public int LODScheduled { get; }

        public int LODCompleted { get; }

        public int DecorateCount { get; }

        public float DecorateMs { get; }

        public long GpuUploadBytes { get; }

        public int GpuUploadCount { get; }

        public int GrowEvents { get; }

        public int InvalidateCount { get; }

        public int GcGen0 { get; }

        public int GcGen1 { get; }

        public int GcGen2 { get; }

        public float MeshCompleteMaxMs { get; }

        public int MeshCompleteStalls { get; }

        public float GenCompleteMaxMs { get; }

        public int GenCompleteStalls { get; }

        public float PollMeshDisposalsMs { get; set; }

        public float PollMeshUploadMs { get; set; }

        public float PollMeshIterateMs { get; set; }

        public float PollMeshFirstIsCompletedMs { get; set; }

        public float SchedMeshFillMs { get; set; }

        public float SchedMeshFilterMs { get; set; }

        public float SchedMeshAllocMs { get; set; }

        public float SchedMeshScheduleMs { get; set; }

        public float SchedMeshFlushMs { get; set; }

        public int TotalGenerated { get; }

        public int TotalMeshed { get; }

        public int TotalLOD { get; }

        public double TotalGpuUploadBytes { get; }

        public void BeginFrame();

        public void IncrGenScheduled();

        public void IncrGenCompleted();

        public void IncrMeshScheduled();

        public void IncrMeshCompleted();

        public void IncrLODScheduled();

        public void IncrLODCompleted();

        public void IncrGrow();

        public void IncrInvalidate();

        public void AddDecorate(float ms);

        public void AddGpuUpload(int bytes);

        public void RecordMeshComplete(float ms);

        public void RecordGenComplete(float ms);
    }
}
