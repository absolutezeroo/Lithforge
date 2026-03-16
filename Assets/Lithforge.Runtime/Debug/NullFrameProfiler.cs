namespace Lithforge.Runtime.Debug
{
    /// <summary>
    /// No-op IFrameProfiler implementation. All Begin/End/BeginFrame calls are
    /// immediate returns. GetMs returns 0. GetHistory returns null.
    /// Use in headless/test scenarios where profiling is not needed.
    /// </summary>
    public sealed class NullFrameProfiler : IFrameProfiler
    {
        public bool Enabled
        {
            get { return false; }
            set { }
        }

        public int HistoryHead
        {
            get { return 0; }
        }

        public int HistoryFilled
        {
            get { return 0; }
        }

        public void BeginFrame()
        {
        }

        public void Begin(int sectionIndex)
        {
        }

        public void End(int sectionIndex)
        {
        }

        public float GetMs(int sectionIndex)
        {
            return 0f;
        }

        public float[] GetHistory(int sectionIndex)
        {
            return null;
        }
    }
}
