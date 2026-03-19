namespace Lithforge.Runtime.Debug
{
    /// <summary>
    /// No-op IFrameProfiler implementation. All Begin/End/BeginFrame calls are
    /// immediate returns. GetMs returns 0. GetHistory returns null.
    /// Use in headless/test scenarios where profiling is not needed.
    /// </summary>
    public sealed class NullFrameProfiler : IFrameProfiler
    {
        /// <summary>Always returns false. Setting has no effect.</summary>
        public bool Enabled
        {
            get { return false; }
            set { }
        }

        /// <summary>Always returns 0.</summary>
        public int HistoryHead
        {
            get { return 0; }
        }

        /// <summary>Always returns 0.</summary>
        public int HistoryFilled
        {
            get { return 0; }
        }

        /// <summary>No-op.</summary>
        public void BeginFrame()
        {
        }

        /// <summary>No-op.</summary>
        public void Begin(int sectionIndex)
        {
        }

        /// <summary>No-op.</summary>
        public void End(int sectionIndex)
        {
        }

        /// <summary>Always returns 0f.</summary>
        public float GetMs(int sectionIndex)
        {
            return 0f;
        }

        /// <summary>Always returns null.</summary>
        public float[] GetHistory(int sectionIndex)
        {
            return null;
        }
    }
}
