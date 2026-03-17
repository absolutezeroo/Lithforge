using Lithforge.Runtime.Scheduling;

namespace Lithforge.Runtime.Tick
{
    /// <summary>
    /// ITickable adapter that counts ticks and triggers
    /// <see cref="LiquidScheduler.OnSimTick"/> every
    /// <see cref="LiquidConstants.SimTickInterval"/> ticks (~3.75 Hz at 30 TPS).
    /// </summary>
    public sealed class LiquidTickAdapter : ITickable
    {
        private readonly LiquidScheduler _liquidScheduler;
        private int _tickCounter;

        public LiquidTickAdapter(LiquidScheduler liquidScheduler)
        {
            _liquidScheduler = liquidScheduler;
            _tickCounter = 0;
        }

        public void Tick(float tickDt)
        {
            _tickCounter++;

            if (_tickCounter >= LiquidConstants.SimTickInterval)
            {
                _tickCounter = 0;
                _liquidScheduler.OnSimTick();
            }
        }
    }
}
