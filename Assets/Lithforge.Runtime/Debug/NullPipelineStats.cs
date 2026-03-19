namespace Lithforge.Runtime.Debug
{
    /// <summary>
    ///     No-op IPipelineStats implementation. All increment methods are immediate returns.
    ///     All counters read as zero. Use in headless/test scenarios.
    /// </summary>
    public sealed class NullPipelineStats : IPipelineStats
    {
        /// <summary>Always returns false. Setting has no effect.</summary>
        public bool Enabled
        {
            get { return false; }
            set { }
        }

        /// <summary>No-op.</summary>
        public void BeginFrame() { }

        /// <summary>No-op.</summary>
        public void IncrGenScheduled() { }

        /// <summary>No-op.</summary>
        public void IncrGenCompleted() { }

        /// <summary>No-op.</summary>
        public void IncrMeshScheduled() { }

        /// <summary>No-op.</summary>
        public void IncrMeshCompleted() { }

        /// <summary>No-op.</summary>
        public void IncrLODScheduled() { }

        /// <summary>No-op.</summary>
        public void IncrLODCompleted() { }

        /// <summary>No-op.</summary>
        public void IncrGrow() { }

        /// <summary>No-op.</summary>
        public void IncrInvalidate() { }

        /// <summary>No-op.</summary>
        public void AddDecorate(float ms) { }

        /// <summary>No-op.</summary>
        public void AddGpuUpload(int bytes) { }

        /// <summary>No-op.</summary>
        public void RecordMeshComplete(float ms) { }

        /// <summary>No-op.</summary>
        public void RecordGenComplete(float ms) { }

        /// <summary>Always returns 0.</summary>
        public int GenScheduled { get { return 0; } }

        /// <summary>Always returns 0.</summary>
        public int GenCompleted { get { return 0; } }

        /// <summary>Always returns 0.</summary>
        public int MeshScheduled { get { return 0; } }

        /// <summary>Always returns 0.</summary>
        public int MeshCompleted { get { return 0; } }

        /// <summary>Always returns 0.</summary>
        public int LODScheduled { get { return 0; } }

        /// <summary>Always returns 0.</summary>
        public int LODCompleted { get { return 0; } }

        /// <summary>Always returns 0.</summary>
        public int DecorateCount { get { return 0; } }

        /// <summary>Always returns 0f.</summary>
        public float DecorateMs { get { return 0f; } }

        /// <summary>Always returns 0.</summary>
        public long GpuUploadBytes { get { return 0; } }

        /// <summary>Always returns 0.</summary>
        public int GpuUploadCount { get { return 0; } }

        /// <summary>Always returns 0.</summary>
        public int GrowEvents { get { return 0; } }

        /// <summary>Always returns 0.</summary>
        public int InvalidateCount { get { return 0; } }

        /// <summary>Always returns 0.</summary>
        public int GcGen0 { get { return 0; } }

        /// <summary>Always returns 0.</summary>
        public int GcGen1 { get { return 0; } }

        /// <summary>Always returns 0.</summary>
        public int GcGen2 { get { return 0; } }

        /// <summary>Always returns 0f.</summary>
        public float MeshCompleteMaxMs { get { return 0f; } }

        /// <summary>Always returns 0.</summary>
        public int MeshCompleteStalls { get { return 0; } }

        /// <summary>Always returns 0f.</summary>
        public float GenCompleteMaxMs { get { return 0f; } }

        /// <summary>Always returns 0.</summary>
        public int GenCompleteStalls { get { return 0; } }

        /// <summary>Always returns 0f. Setting has no effect.</summary>
        public float PollMeshDisposalsMs { get { return 0f; } set { } }

        /// <summary>Always returns 0f. Setting has no effect.</summary>
        public float PollMeshUploadMs { get { return 0f; } set { } }

        /// <summary>Always returns 0f. Setting has no effect.</summary>
        public float PollMeshIterateMs { get { return 0f; } set { } }

        /// <summary>Always returns 0f. Setting has no effect.</summary>
        public float PollMeshFirstIsCompletedMs { get { return 0f; } set { } }

        /// <summary>Always returns 0f. Setting has no effect.</summary>
        public float SchedMeshFillMs { get { return 0f; } set { } }

        /// <summary>Always returns 0f. Setting has no effect.</summary>
        public float SchedMeshFilterMs { get { return 0f; } set { } }

        /// <summary>Always returns 0f. Setting has no effect.</summary>
        public float SchedMeshAllocMs { get { return 0f; } set { } }

        /// <summary>Always returns 0f. Setting has no effect.</summary>
        public float SchedMeshScheduleMs { get { return 0f; } set { } }

        /// <summary>Always returns 0f. Setting has no effect.</summary>
        public float SchedMeshFlushMs { get { return 0f; } set { } }

        /// <summary>Always returns 0.</summary>
        public int TotalGenerated { get { return 0; } }

        /// <summary>Always returns 0.</summary>
        public int TotalMeshed { get { return 0; } }

        /// <summary>Always returns 0.</summary>
        public int TotalLOD { get { return 0; } }

        /// <summary>Always returns 0.0.</summary>
        public double TotalGpuUploadBytes { get { return 0.0; } }
    }
}
