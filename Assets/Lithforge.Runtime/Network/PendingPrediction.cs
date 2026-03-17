using Lithforge.Voxel.Block;
using Unity.Mathematics;

namespace Lithforge.Runtime.Network
{
    /// <summary>
    /// Tracks a pending optimistic block prediction awaiting server acknowledgement.
    /// </summary>
    internal struct PendingPrediction
    {
        public int3 Position;
        public StateId OldState;
    }
}
