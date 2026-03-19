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
        private readonly ClientBlockPredictor _predictor;

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