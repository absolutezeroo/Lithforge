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

        private static Stopwatch[] s_stopwatches;
        private static float[] s_currentMs;
        private static float[][] s_history;
        private static int s_historyHead;
        private static int s_historyFilled;
        private static bool s_initialized;
        private static double s_ticksToMs;

        /// <summary>
        /// Allocates all internal arrays. Call once at startup.
        /// </summary>
        public static void Init()
        {
            s_stopwatches = new Stopwatch[SectionCount];
            s_currentMs = new float[SectionCount];
            s_history = new float[SectionCount][];

            for (int i = 0; i < SectionCount; i++)
            {
                s_stopwatches[i] = new Stopwatch();
                s_history[i] = new float[HistorySize];
            }

            s_historyHead = 0;
            s_historyFilled = 0;
            s_ticksToMs = 1000.0 / Stopwatch.Frequency;
            s_initialized = true;
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
            if (!Enabled || !s_initialized)
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
                    ms = (float)(s_stopwatches[i].ElapsedTicks * s_ticksToMs);
                }

                s_currentMs[i] = ms;
                s_history[i][s_historyHead] = ms;
                s_stopwatches[i].Reset();
            }

            s_historyHead = (s_historyHead + 1) % HistorySize;

            if (s_historyFilled < HistorySize)
            {
                s_historyFilled++;
            }
        }

        /// <summary>
        /// Starts timing the given section. Pairs with End(sectionIndex).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Begin(int sectionIndex)
        {
            if (!Enabled || !s_initialized)
            {
                return;
            }

            s_stopwatches[sectionIndex].Start();
        }

        /// <summary>
        /// Stops timing the given section. Pairs with Begin(sectionIndex).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void End(int sectionIndex)
        {
            if (!Enabled || !s_initialized)
            {
                return;
            }

            s_stopwatches[sectionIndex].Stop();
        }

        /// <summary>
        /// Returns the most recent frame's measurement for the given section in milliseconds.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetMs(int sectionIndex)
        {
            if (!s_initialized)
            {
                return 0f;
            }

            return s_currentMs[sectionIndex];
        }

        /// <summary>
        /// Returns the rolling history buffer for the given section.
        /// Use HistoryHead and HistoryFilled to interpret the ring buffer.
        /// </summary>
        public static float[] GetHistory(int sectionIndex)
        {
            if (!s_initialized)
            {
                return null;
            }

            return s_history[sectionIndex];
        }

        /// <summary>Current write position in the ring buffer (most recent entry is at Head-1).</summary>
        public static int HistoryHead
        {
            get { return s_historyHead; }
        }

        /// <summary>Number of valid entries in the history (0..HistorySize).</summary>
        public static int HistoryFilled
        {
            get { return s_historyFilled; }
        }
    }
}
