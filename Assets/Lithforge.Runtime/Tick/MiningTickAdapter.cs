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
        /// <summary>The block interaction component to tick.</summary>
        private readonly BlockInteraction _blockInteraction;

        /// <summary>Creates a mining tick adapter wrapping the given block interaction.</summary>
        public MiningTickAdapter(BlockInteraction blockInteraction)
        {
            _blockInteraction = blockInteraction;
        }

        /// <summary>Advances mining progress by one fixed tick interval.</summary>
        public void Tick(float tickDt)
        {
            _blockInteraction.TickMining(tickDt);
        }
    }
}
