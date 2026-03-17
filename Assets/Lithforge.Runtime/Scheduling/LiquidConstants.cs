namespace Lithforge.Runtime.Scheduling
{
    /// <summary>
    /// Constants for liquid simulation scheduling.
    /// </summary>
    public static class LiquidConstants
    {
        /// <summary>Run liquid sim every N game ticks. 8 ticks at 30 TPS = ~3.75 Hz.</summary>
        public const int SimTickInterval = 8;

        /// <summary>Max liquid jobs to schedule per sim tick.</summary>
        public const int MaxJobsPerTick = 32;

        /// <summary>Max liquid job completions to process per Update() frame.</summary>
        public const int MaxCompletionsPerFrame = 16;

        /// <summary>Max radius (in chunks) from camera to run liquid sim.</summary>
        public const int SimRadius = 6;

        /// <summary>Max frames to wait before force-completing a liquid job.</summary>
        public const int MaxJobFrameAge = 300;
    }
}
