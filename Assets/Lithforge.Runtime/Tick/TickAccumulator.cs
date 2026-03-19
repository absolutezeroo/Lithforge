namespace Lithforge.Runtime.Tick
{
    /// <summary>
    /// Fixed-timestep accumulator. Store as a field; call Accumulate() each frame,
    /// then loop on ShouldTick() + ConsumeOneTick() until false.
    /// </summary>
    public struct TickAccumulator
    {
        /// <summary>Elapsed time accumulated but not yet consumed by ticks.</summary>
        private float _accumulated;

        /// <summary>
        /// Adds elapsed time. Clamps to MaxAccumulatedTime to prevent spiral-of-death.
        /// </summary>
        public void Accumulate(float deltaTime)
        {
            _accumulated += deltaTime;

            if (_accumulated > FixedTickRate.MaxAccumulatedTime)
            {
                _accumulated = FixedTickRate.MaxAccumulatedTime;
            }
        }

        /// <summary>True when there is at least one full tick worth of time to consume.</summary>
        public bool ShouldTick
        {
            get { return _accumulated >= FixedTickRate.TickDeltaTime; }
        }

        /// <summary>Subtracts one tick interval from the accumulator.</summary>
        public void ConsumeOneTick()
        {
            _accumulated -= FixedTickRate.TickDeltaTime;

            if (_accumulated < 0f)
            {
                _accumulated = 0f;
            }
        }

        /// <summary>
        /// Fraction [0,1) of time since the last tick. Used for position interpolation.
        /// </summary>
        public float Alpha
        {
            get
            {
                float alpha = _accumulated / FixedTickRate.TickDeltaTime;

                return alpha < 0f ? 0f : alpha;
            }
        }
    }
}
