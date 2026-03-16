namespace Lithforge.Runtime.Debug
{
    /// <summary>
    ///     Compile-time constants and metadata for FrameProfiler sections.
    ///     Use these indices with IFrameProfiler.Begin() / End() / GetMs().
    ///     Section indices are stable identifiers — do not reorder.
    /// </summary>
    public static class FrameProfilerSections
    {
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
        public const int TickLoop = 14;
        public const int SectionCount = 15;
        public const int HistorySize = 300;

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
