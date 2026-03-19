using Lithforge.Runtime.BlockEntity;

namespace Lithforge.Runtime.Tick
{
    /// <summary>
    /// Wraps BlockEntityTickScheduler for participation in the fixed tick loop.
    /// Replaces the old Tick(Time.deltaTime) call in GameLoop.Update().
    /// </summary>
    public sealed class BlockEntityTickAdapter : ITickable
    {
        /// <summary>The block entity tick scheduler to tick.</summary>
        private readonly BlockEntityTickScheduler _scheduler;

        /// <summary>Creates a block entity tick adapter wrapping the given scheduler.</summary>
        public BlockEntityTickAdapter(BlockEntityTickScheduler scheduler)
        {
            _scheduler = scheduler;
        }

        /// <summary>Ticks block entities using the round-robin scheduler.</summary>
        public void Tick(float tickDt)
        {
            _scheduler.Tick(tickDt);
        }
    }
}
