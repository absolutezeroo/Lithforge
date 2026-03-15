namespace Lithforge.Runtime.Tick
{
    /// <summary>
    /// Canonical fixed tick-rate constants for Lithforge.
    /// All game simulation advances in TickDeltaTime increments.
    /// </summary>
    public static class FixedTickRate
    {
        /// <summary>Ticks per second.</summary>
        public const int TicksPerSecond = 30;

        /// <summary>Fixed simulation delta per tick in seconds (~33.33ms).</summary>
        public const float TickDeltaTime = 1f / TicksPerSecond;

        /// <summary>
        /// Maximum seconds of accumulated time to drain per frame.
        /// Prevents the spiral of death — caps at 5 ticks per frame.
        /// </summary>
        public const float MaxAccumulatedTime = TickDeltaTime * 5;
    }
}
