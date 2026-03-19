using Lithforge.Voxel.Block;

using Unity.Mathematics;

namespace Lithforge.Runtime.Network
{
    /// <summary>
    ///     Tracks a pending optimistic block prediction awaiting server acknowledgement.
    ///     Includes a timestamp for expiry when the ACK is never received.
    /// </summary>
    internal struct PendingPrediction
    {
        /// <summary>World-space block coordinate of the predicted change.</summary>
        public int3 Position;

        /// <summary>Block state before the prediction, used for revert on reject or expiry.</summary>
        public StateId OldState;

        /// <summary>
        ///     Realtime timestamp (Time.realtimeSinceStartup) when the prediction was recorded.
        ///     Used by the expiry sweep to revert predictions whose ACK was never received.
        /// </summary>
        public float Timestamp;
    }
}