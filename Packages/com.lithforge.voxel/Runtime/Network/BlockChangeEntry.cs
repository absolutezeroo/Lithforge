using System.Runtime.InteropServices;
using Lithforge.Voxel.Block;
using Unity.Mathematics;

namespace Lithforge.Voxel.Network
{
    /// <summary>
    /// Single block change for network delta sync.
    /// Stores world-space coordinates and the new block state.
    /// Sent individually or batched per chunk section.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BlockChangeEntry
    {
        /// <summary>World-space block coordinate.</summary>
        public int3 Position;

        /// <summary>New block state.</summary>
        public StateId NewState;
    }
}
