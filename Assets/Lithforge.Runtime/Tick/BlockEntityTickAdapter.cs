using Lithforge.Runtime.BlockEntity;

namespace Lithforge.Runtime.Tick
{
    /// <summary>
    /// Wraps BlockEntityTickScheduler for participation in the fixed tick loop.
    /// Replaces the old Tick(Time.deltaTime) call in GameLoop.Update().
    /// </summary>
    public sealed class BlockEntityTickAdapter : ITickable
    {
        private readonly BlockEntityTickScheduler _scheduler;

        public BlockEntityTickAdapter(BlockEntityTickScheduler scheduler)
        {
            _scheduler = scheduler;
        }

        public void Tick(float tickDt)
        {
            _scheduler.Tick(tickDt);
        }
    }
}
