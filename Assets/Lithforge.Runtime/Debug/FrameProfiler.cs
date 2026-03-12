using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Lithforge.Runtime.Debug
{
    /// <summary>
    /// Static frame profiler that measures CPU cost of each GameLoop section.
    /// Uses System.Diagnostics.Stopwatch for sub-microsecond precision with zero GC.
    /// All arrays are pre-allocated at Init(). Zero allocation per frame.
    /// Gated by static bool Enabled — when false, Begin/End are near-zero-cost
    /// (single branch on a static bool, predicted correctly after first call).
    /// </summary>
    public static class FrameProfiler
    {
        // Section index constants — used as array indices, never as strings at runtime
        public const int PollGen = 0;
        public const int PollMesh = 1;
        public const int PollLOD = 2;
        public const int LoadQueue = 3;
        public const int Unload = 4;
        public const int SchedGen = 5;
        public const int CrossLight = 6;
        public const int Relight = 7;
        public const int LODLevels = 8;
        public const int SchedMesh = 9;
        public const int SchedLOD = 10;
        public const int Render = 11;
        public const int UpdateTotal = 12;
        public const int Frame = 13;
        public const int SectionCount = 14;

        /// <summary>Number of frames stored in the rolling history buffer.</summary>
        public const int HistorySize = 300;

        /// <summary>Human-readable names for each section. Indexed by section constant.</summary>
        public static readonly string[] SectionNames = new string[]
        {
            "PollGen",
            "PollMesh",
            "PollLOD",
            "LoadQueue",
            "Unload",
            "SchedGen",
            "CrossLight",
            "Relight",
            "LODLevels",
            "SchedMesh",
            "SchedLOD",
            "Render",
            "UpdateTotal",
            "Frame",
        };

        public static bool Enabled;

        private static Stopwatch[] _stopwatches;
        private static float[] _currentMs;
        private static float[][] _history;
        private static int _historyHead;
        private static int _historyFilled;
        private static bool _initialized;
        private static double _ticksToMs;

        /// <summary>
        /// Allocates all internal arrays. Call once at startup.
        /// </summary>
        public static void Init()
        {
            _stopwatches = new Stopwatch[SectionCount];
            _currentMs = new float[SectionCount];
            _history = new float[SectionCount][];

            for (int i = 0; i < SectionCount; i++)
            {
                _stopwatches[i] = new Stopwatch();
                _history[i] = new float[HistorySize];
            }

            _historyHead = 0;
            _historyFilled = 0;
            _ticksToMs = 1000.0 / Stopwatch.Frequency;
            _initialized = true;
        }

        /// <summary>
        /// Called at the start of each frame. Stores previous frame's measurements
        /// into the rolling history and resets all stopwatches.
        /// The Frame section is set from unscaledDeltaTime (total frame time
        /// including rendering, vsync, etc. — not measurable via Stopwatch).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginFrame()
        {
            if (!Enabled || !_initialized)
            {
                return;
            }

            // Record total frame time from Unity's delta (includes everything)
            float frameDeltaMs = UnityEngine.Time.unscaledDeltaTime * 1000f;

            // Store previous frame's results into history ring buffer
            for (int i = 0; i < SectionCount; i++)
            {
                float ms;

                if (i == Frame)
                {
                    ms = frameDeltaMs;
                }
                else
                {
                    ms = (float)(_stopwatches[i].ElapsedTicks * _ticksToMs);
                }

                _currentMs[i] = ms;
                _history[i][_historyHead] = ms;
                _stopwatches[i].Reset();
            }

            _historyHead = (_historyHead + 1) % HistorySize;

            if (_historyFilled < HistorySize)
            {
                _historyFilled++;
            }
        }

        /// <summary>
        /// Starts timing the given section. Pairs with End(sectionIndex).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Begin(int sectionIndex)
        {
            if (!Enabled || !_initialized)
            {
                return;
            }

            _stopwatches[sectionIndex].Start();
        }

        /// <summary>
        /// Stops timing the given section. Pairs with Begin(sectionIndex).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void End(int sectionIndex)
        {
            if (!Enabled || !_initialized)
            {
                return;
            }

            _stopwatches[sectionIndex].Stop();
        }

        /// <summary>
        /// Returns the most recent frame's measurement for the given section in milliseconds.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetMs(int sectionIndex)
        {
            if (!_initialized)
            {
                return 0f;
            }

            return _currentMs[sectionIndex];
        }

        /// <summary>
        /// Returns the rolling history buffer for the given section.
        /// Use HistoryHead and HistoryFilled to interpret the ring buffer.
        /// </summary>
        public static float[] GetHistory(int sectionIndex)
        {
            if (!_initialized)
            {
                return null;
            }

            return _history[sectionIndex];
        }

        /// <summary>Current write position in the ring buffer (most recent entry is at Head-1).</summary>
        public static int HistoryHead
        {
            get { return _historyHead; }
        }

        /// <summary>Number of valid entries in the history (0..HistorySize).</summary>
        public static int HistoryFilled
        {
            get { return _historyFilled; }
        }
    }
}
