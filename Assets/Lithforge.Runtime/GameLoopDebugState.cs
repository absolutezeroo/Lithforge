using Lithforge.Runtime.Debug;

namespace Lithforge.Runtime
{
    /// <summary>
    ///     Groups all debug/profiling references injected into GameLoop.
    /// </summary>
    public sealed class GameLoopDebugState
    {
        public IFrameProfiler FrameProfiler { get; set; } = new NullFrameProfiler();

        public IPipelineStats PipelineStats { get; set; } = new NullPipelineStats();

        public MetricsRegistry MetricsRegistry { get; set; }
    }
}
