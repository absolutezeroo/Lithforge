namespace Lithforge.Runtime.Debug.Benchmark
{
    /// <summary>
    /// Stores the computed results of a benchmark run: timing stats, pipeline throughput,
    /// bottleneck detection, and pass/fail status.
    /// </summary>
    public sealed class BenchmarkResult
    {
        /// <summary>Display name of the scenario that produced this result.</summary>
        public string ScenarioName;

        /// <summary>Number of frames captured during the benchmark run.</summary>
        public int TotalFrames;

        /// <summary>Wall-clock duration of the entire benchmark run in seconds.</summary>
        public float DurationSeconds;

        // Frame time stats

        /// <summary>Mean frame time across all captured frames in milliseconds.</summary>
        public float AvgFrameMs;

        /// <summary>Fastest frame time observed during the run.</summary>
        public float MinFrameMs;

        /// <summary>Slowest frame time observed during the run.</summary>
        public float MaxFrameMs;

        /// <summary>1st percentile frame time -- best-case performance floor.</summary>
        public float P1FrameMs;

        /// <summary>99th percentile frame time -- worst-case performance ceiling (stutter indicator).</summary>
        public float P99FrameMs;

        // FPS stats

        /// <summary>Mean frames-per-second across the run.</summary>
        public float AvgFps;

        /// <summary>Lowest instantaneous FPS observed (corresponds to the worst frame).</summary>
        public float MinFps;

        /// <summary>Highest instantaneous FPS observed (corresponds to the best frame).</summary>
        public float MaxFps;

        /// <summary>1st percentile FPS -- sustained worst-case throughput.</summary>
        public float P1Fps;

        /// <summary>99th percentile FPS -- sustained best-case throughput.</summary>
        public float P99Fps;

        // Pipeline throughput totals

        /// <summary>Total worldgen jobs completed during the benchmark.</summary>
        public int TotalGenerated;

        /// <summary>Total LOD0 mesh jobs completed during the benchmark.</summary>
        public int TotalMeshed;

        /// <summary>Cumulative bytes uploaded to GPU during the benchmark.</summary>
        public long TotalGpuUploadBytes;

        /// <summary>Number of MegaMeshBuffer grow (reallocation) events during the benchmark.</summary>
        public int TotalGrowEvents;

        // Top costs (section index, avg ms)

        /// <summary>FrameProfiler section indices of the most expensive pipeline stages, sorted by cost.</summary>
        public int[] TopSectionIndices;

        /// <summary>Average milliseconds per frame for each top-cost section, parallel with TopSectionIndices.</summary>
        public float[] TopSectionAvgMs;

        // Bottleneck

        /// <summary>Human-readable description of the detected performance bottleneck, if any.</summary>
        public string BottleneckDescription;

        // Pass/fail

        /// <summary>Threshold: the maximum acceptable average frame time in milliseconds for a passing result.</summary>
        public float MaxAvgFrameTimeMs;

        /// <summary>Whether the benchmark met its target frame time (AvgFrameMs &lt;= MaxAvgFrameTimeMs).</summary>
        public bool Passed;

        // Pre-allocated parallel arrays for per-frame data (owned by BenchmarkRunner, referenced here)

        /// <summary>Per-frame wall-clock time in milliseconds, indexed by frame number.</summary>
        public float[] FrameMs;

        /// <summary>
        /// Per-frame FrameProfiler section timings. First dimension is section index,
        /// second is frame number (SectionMs[section][frame]).
        /// </summary>
        public float[][] SectionMs;

        /// <summary>Per-frame worldgen jobs completed.</summary>
        public int[] GenCompleted;

        /// <summary>Per-frame LOD0 mesh jobs completed.</summary>
        public int[] MeshCompleted;

        /// <summary>Per-frame LOD>0 mesh jobs completed.</summary>
        public int[] LodCompleted;

        /// <summary>Per-frame bytes uploaded to GPU.</summary>
        public long[] GpuUploadBytes;

        /// <summary>Per-frame GPU upload operation count.</summary>
        public int[] GpuUploadCount;

        /// <summary>Per-frame MegaMeshBuffer grow events.</summary>
        public int[] GrowEvents;

        /// <summary>Per-frame GC generation-0 collection count.</summary>
        public int[] GcGen0;

        /// <summary>Per-frame GC generation-1 collection count.</summary>
        public int[] GcGen1;

        /// <summary>Per-frame GC generation-2 (full) collection count.</summary>
        public int[] GcGen2;

        // Pipeline counters (per-frame)

        /// <summary>Per-frame worldgen jobs scheduled.</summary>
        public int[] GenScheduled;

        /// <summary>Per-frame LOD0 mesh jobs scheduled.</summary>
        public int[] MeshScheduled;

        /// <summary>Per-frame LOD>0 mesh jobs scheduled.</summary>
        public int[] LodScheduled;

        /// <summary>Per-frame chunk mesh invalidation count.</summary>
        public int[] InvalidateCount;

        /// <summary>Per-frame slowest mesh Complete() call in milliseconds.</summary>
        public float[] MeshCompleteMaxMs;

        /// <summary>Per-frame count of mesh Complete() calls that exceeded 1ms.</summary>
        public int[] MeshCompleteStalls;

        /// <summary>Per-frame slowest generation Complete() call in milliseconds.</summary>
        public float[] GenCompleteMaxMs;

        /// <summary>Per-frame count of generation Complete() calls that exceeded 1ms.</summary>
        public int[] GenCompleteStalls;

        // SchedMesh sub-timings (per-frame ms)

        /// <summary>Per-frame time spent filling the mesh candidate list.</summary>
        public float[] SchedMeshFillMs;

        /// <summary>Per-frame time spent filtering and sorting mesh candidates.</summary>
        public float[] SchedMeshFilterMs;

        /// <summary>Per-frame time spent allocating NativeArrays for mesh output.</summary>
        public float[] SchedMeshAllocMs;

        /// <summary>Per-frame time spent calling Schedule() on mesh jobs.</summary>
        public float[] SchedMeshScheduleMs;

        /// <summary>Per-frame time spent flushing completed mesh data to GPU buffers.</summary>
        public float[] SchedMeshFlushMs;

        // Index sizes (per-frame)

        /// <summary>Per-frame size of the Generated state set (chunks eligible for meshing).</summary>
        public int[] GeneratedSetSize;
    }
}
