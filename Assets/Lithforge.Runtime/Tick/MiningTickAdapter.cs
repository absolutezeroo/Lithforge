using Lithforge.Runtime.Input;

namespace Lithforge.Runtime.Tick
{
    /// <summary>
    /// Drives BlockInteraction.TickMining() at fixed rate from GameLoop's tick loop.
    /// Mining progress accumulates in fixed dt increments instead of frame dt.
    /// BlockInteraction still handles raycasting and placement at frame rate.
    /// </summary>
    public sealed class MiningTickAdapter : ITickable
    {
        private readonly BlockInteraction _blockInteraction;

        public MiningTickAdapter(BlockInteraction blockInteraction)
        {
            _blockInteraction = blockInteraction;
        }

        public void Tick(float tickDt)
        {
            _blockInteraction.TickMining(tickDt);
        }
    }
}
