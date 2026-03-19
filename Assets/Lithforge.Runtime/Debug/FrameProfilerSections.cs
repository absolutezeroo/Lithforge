namespace Lithforge.Runtime.Debug
{
    /// <summary>
    ///     Compile-time constants and metadata for FrameProfiler sections.
    ///     Use these indices with IFrameProfiler.Begin() / End() / GetMs().
    ///     Section indices are stable identifiers — do not reorder.
    /// </summary>
    public static class FrameProfilerSections
    {
        /// <summary>Section index for polling completed generation jobs.</summary>
        public const int PollGen = 0;

        /// <summary>Section index for polling completed mesh jobs.</summary>
        public const int PollMesh = 1;

        /// <summary>Section index for polling completed LOD mesh jobs.</summary>
        public const int PollLOD = 2;

        /// <summary>Section index for processing the chunk load queue.</summary>
        public const int LoadQueue = 3;

        /// <summary>Section index for unloading out-of-range chunks.</summary>
        public const int Unload = 4;

        /// <summary>Section index for scheduling new generation jobs.</summary>
        public const int SchedGen = 5;

        /// <summary>Section index for cross-chunk light propagation.</summary>
        public const int CrossLight = 6;

        /// <summary>Section index for relighting chunks after block edits.</summary>
        public const int Relight = 7;

        /// <summary>Section index for updating LOD level assignments.</summary>
        public const int LODLevels = 8;

        /// <summary>Section index for scheduling LOD0 mesh jobs.</summary>
        public const int SchedMesh = 9;

        /// <summary>Section index for scheduling LOD>0 mesh jobs.</summary>
        public const int SchedLOD = 10;

        /// <summary>Section index for GPU rendering (draw calls, culling).</summary>
        public const int Render = 11;

        /// <summary>Section index for total Update() time (aggregate of all sections).</summary>
        public const int UpdateTotal = 12;

        /// <summary>Section index for total frame time including non-Update work.</summary>
        public const int Frame = 13;

        /// <summary>Section index for the fixed tick loop execution time.</summary>
        public const int TickLoop = 14;

        /// <summary>Total number of profiler sections.</summary>
        public const int SectionCount = 15;

        /// <summary>Number of frames stored in the rolling history ring buffer.</summary>
        public const int HistorySize = 300;

        /// <summary>Human-readable names for each section, indexed by section constant.</summary>
        public static readonly string[] SectionNames =
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
            "TickLoop",
        };
    }
}
