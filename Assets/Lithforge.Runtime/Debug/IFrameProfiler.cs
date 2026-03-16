namespace Lithforge.Runtime.Debug
{
    /// <summary>
    ///     Per-session frame profiler. Measures CPU cost of each GameLoop section
    ///     using Stopwatch, maintaining a rolling history.
    ///     Use FrameProfilerSections constants as section indices.
    ///     Owner: LithforgeBootstrap. Injected into GameLoop, MetricsRegistry,
    ///     F3DebugOverlay, and BenchmarkRunner.
    /// </summary>
    public interface IFrameProfiler
    {
        /// <summary>
        ///     Enables or disables profiling. When false, Begin/End/BeginFrame are no-ops.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>Current write position in the history ring buffer.</summary>
        public int HistoryHead { get; }

        /// <summary>Number of valid entries filled in the history (0..HistorySize).</summary>
        public int HistoryFilled { get; }

        /// <summary>
        ///     Commits the previous frame's stopwatch results into the history ring buffer
        ///     and resets all stopwatches. Call once at the start of Update.
        /// </summary>
        public void BeginFrame();

        /// <summary>
        ///     Starts timing section <paramref name="sectionIndex" />.
        /// </summary>
        public void Begin(int sectionIndex);

        /// <summary>
        ///     Stops timing section <paramref name="sectionIndex" />.
        /// </summary>
        public void End(int sectionIndex);

        /// <summary>
        ///     Returns the most recent completed frame's measurement for
        ///     <paramref name="sectionIndex" /> in milliseconds.
        /// </summary>
        public float GetMs(int sectionIndex);

        /// <summary>
        ///     Returns the rolling history buffer for <paramref name="sectionIndex" />.
        ///     Length is FrameProfilerSections.HistorySize. Interpret with HistoryHead
        ///     and HistoryFilled. May return null if not initialized.
        /// </summary>
        public float[] GetHistory(int sectionIndex);
    }
}
