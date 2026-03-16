namespace Lithforge.Runtime.Debug
{
    /// <summary>
    ///     No-op IPipelineStats implementation. All increment methods are immediate returns.
    ///     All counters read as zero. Use in headless/test scenarios.
    /// </summary>
    public sealed class NullPipelineStats : IPipelineStats
    {
        public bool Enabled
        {
            get { return false; }
            set { }
        }

        public void BeginFrame() { }

        public void IncrGenScheduled() { }

        public void IncrGenCompleted() { }

        public void IncrMeshScheduled() { }

        public void IncrMeshCompleted() { }

        public void IncrLODScheduled() { }

        public void IncrLODCompleted() { }

        public void IncrGrow() { }

        public void IncrInvalidate() { }

        public void AddDecorate(float ms) { }

        public void AddGpuUpload(int bytes) { }

        public void RecordMeshComplete(float ms) { }

        public void RecordGenComplete(float ms) { }

        public int GenScheduled { get { return 0; } }

        public int GenCompleted { get { return 0; } }

        public int MeshScheduled { get { return 0; } }

        public int MeshCompleted { get { return 0; } }

        public int LODScheduled { get { return 0; } }

        public int LODCompleted { get { return 0; } }

        public int DecorateCount { get { return 0; } }

        public float DecorateMs { get { return 0f; } }

        public long GpuUploadBytes { get { return 0; } }

        public int GpuUploadCount { get { return 0; } }

        public int GrowEvents { get { return 0; } }

        public int InvalidateCount { get { return 0; } }

        public int GcGen0 { get { return 0; } }

        public int GcGen1 { get { return 0; } }

        public int GcGen2 { get { return 0; } }

        public float MeshCompleteMaxMs { get { return 0f; } }

        public int MeshCompleteStalls { get { return 0; } }

        public float GenCompleteMaxMs { get { return 0f; } }

        public int GenCompleteStalls { get { return 0; } }

        public float PollMeshDisposalsMs { get { return 0f; } set { } }

        public float PollMeshUploadMs { get { return 0f; } set { } }

        public float PollMeshIterateMs { get { return 0f; } set { } }

        public float PollMeshFirstIsCompletedMs { get { return 0f; } set { } }

        public float SchedMeshFillMs { get { return 0f; } set { } }

        public float SchedMeshFilterMs { get { return 0f; } set { } }

        public float SchedMeshAllocMs { get { return 0f; } set { } }

        public float SchedMeshScheduleMs { get { return 0f; } set { } }

        public float SchedMeshFlushMs { get { return 0f; } set { } }

        public int TotalGenerated { get { return 0; } }

        public int TotalMeshed { get { return 0; } }

        public int TotalLOD { get { return 0; } }

        public double TotalGpuUploadBytes { get { return 0.0; } }
    }
}
