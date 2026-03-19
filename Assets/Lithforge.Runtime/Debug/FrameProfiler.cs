using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Lithforge.Runtime.Debug
{
    /// <summary>
    /// Stopwatch-based frame profiler implementing IFrameProfiler.
    /// Zero GC per frame. All arrays pre-allocated in constructor.
    /// Gated by Enabled — when false, Begin/End/BeginFrame branch once
    /// on an instance bool and return.
    /// Owner: LithforgeBootstrap. Lifetime: application session.
    /// </summary>
    public sealed class FrameProfiler : IFrameProfiler
    {
        private readonly Stopwatch[] _stopwatches;
        private readonly float[] _currentMs;
        private readonly float[][] _history;
        private readonly double _ticksToMs;

        public bool Enabled { get; set; }

        public int HistoryHead { get; private set; }

        public int HistoryFilled { get; private set; }

        public FrameProfiler()
        {
            int count = FrameProfilerSections.SectionCount;
            _stopwatches = new Stopwatch[count];
            _currentMs = new float[count];
            _history = new float[count][];

            for (int i = 0; i < count; i++)
            {
                _stopwatches[i] = new Stopwatch();
                _history[i] = new float[FrameProfilerSections.HistorySize];
            }

            _ticksToMs = 1000.0 / Stopwatch.Frequency;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginFrame()
        {
            if (!Enabled)
            {
                return;
            }

            float frameDeltaMs = UnityEngine.Time.unscaledDeltaTime * 1000f;
            int count = FrameProfilerSections.SectionCount;

            for (int i = 0; i < count; i++)
            {
                float ms;

                if (i == FrameProfilerSections.Frame)
                {
                    ms = frameDeltaMs;
                }
                else
                {
                    ms = (float)(_stopwatches[i].ElapsedTicks * _ticksToMs);
                }

                _currentMs[i] = ms;
                _history[i][HistoryHead] = ms;
                _stopwatches[i].Reset();
            }

            HistoryHead = (HistoryHead + 1) % FrameProfilerSections.HistorySize;

            if (HistoryFilled < FrameProfilerSections.HistorySize)
            {
                HistoryFilled++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Begin(int sectionIndex)
        {
            if (!Enabled)
            {
                return;
            }

            _stopwatches[sectionIndex].Start();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void End(int sectionIndex)
        {
            if (!Enabled)
            {
                return;
            }

            _stopwatches[sectionIndex].Stop();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetMs(int sectionIndex)
        {
            return _currentMs[sectionIndex];
        }

        public float[] GetHistory(int sectionIndex)
        {
            return _history[sectionIndex];
        }
    }
}
