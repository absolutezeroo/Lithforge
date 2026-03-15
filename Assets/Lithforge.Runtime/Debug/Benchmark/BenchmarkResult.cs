namespace Lithforge.Runtime.Debug.Benchmark
{
    /// <summary>
    /// Stores the computed results of a benchmark run: timing stats, pipeline throughput,
    /// bottleneck detection, and pass/fail status.
    /// </summary>
    public sealed class BenchmarkResult
    {
        public string ScenarioName;
        public int TotalFrames;
        public float DurationSeconds;

        // Frame time stats
        public float AvgFrameMs;
        public float MinFrameMs;
        public float MaxFrameMs;
        public float P1FrameMs;
        public float P99FrameMs;

        // FPS stats
        public float AvgFps;
        public float MinFps;
        public float MaxFps;
        public float P1Fps;
        public float P99Fps;

        // Pipeline throughput totals
        public int TotalGenerated;
        public int TotalMeshed;
        public long TotalGpuUploadBytes;
        public int TotalGrowEvents;

        // Top costs (section index, avg ms)
        public int[] TopSectionIndices;
        public float[] TopSectionAvgMs;

        // Bottleneck
        public string BottleneckDescription;

        // Pass/fail
        public float MaxAvgFrameTimeMs;
        public bool Passed;

        // Pre-allocated parallel arrays for per-frame data (owned by BenchmarkRunner, referenced here)
        public float[] FrameMs;
        public float[][] SectionMs;
        public int[] GenCompleted;
        public int[] MeshCompleted;
        public int[] LodCompleted;
        public long[] GpuUploadBytes;
        public int[] GpuUploadCount;
        public int[] GrowEvents;
        public int[] GcGen0;
        public int[] GcGen1;
        public int[] GcGen2;

        // Pipeline counters (per-frame)
        public int[] GenScheduled;
        public int[] MeshScheduled;
        public int[] LodScheduled;
        public int[] InvalidateCount;
        public float[] MeshCompleteMaxMs;
        public int[] MeshCompleteStalls;
        public float[] GenCompleteMaxMs;
        public int[] GenCompleteStalls;

        // SchedMesh sub-timings (per-frame ms)
        public float[] SchedMeshFillMs;
        public float[] SchedMeshFilterMs;
        public float[] SchedMeshAllocMs;
        public float[] SchedMeshScheduleMs;
        public float[] SchedMeshFlushMs;
    }
}
