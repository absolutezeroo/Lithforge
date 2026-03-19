using Lithforge.Runtime.Network;

using UnityEngine;

namespace Lithforge.Runtime.Tick
{
    /// <summary>
    ///     Drives <see cref="ClientBlockPredictor.Tick" /> at fixed rate from the tick loop.
    ///     Expires unacknowledged block predictions to prevent unbounded pending map growth.
    /// </summary>
    public sealed class BlockPredictionTickAdapter : ITickable
    {
        /// <summary>The client block predictor to sweep for expired predictions.</summary>
        private readonly ClientBlockPredictor _predictor;

        /// <summary>Creates a block prediction tick adapter wrapping the given predictor.</summary>
        public BlockPredictionTickAdapter(ClientBlockPredictor predictor)
        {
            _predictor = predictor;
        }

        /// <summary>
        ///     Calls the predictor's expiry sweep with the current realtime.
        /// </summary>
        public void Tick(float tickDt)
        {
            _predictor.Tick(Time.realtimeSinceStartup);
        }
    }
}