using System.Runtime.InteropServices;
using Lithforge.Voxel.Block;
using Unity.Mathematics;

namespace Lithforge.Voxel.Command
{
    /// <summary>
    /// Blittable command for block placement. Position is the target air block
    /// (adjacent to the hit face), not the clicked block.
    /// 24 bytes, Burst-compatible.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PlaceBlockCommand
    {
        /// <summary>Server tick this command targets.</summary>
        public uint Tick;

        /// <summary>Per-player monotonic sequence number for prediction reconciliation.</summary>
        public ushort SequenceId;

        /// <summary>Server-assigned player identifier.</summary>
        public ushort PlayerId;

        /// <summary>World-space block coordinate to place at.</summary>
        public int3 Position;

        /// <summary>State to place (palette ID).</summary>
        public StateId BlockState;

        /// <summary>Which face of the adjacent block was clicked.</summary>
        public BlockFace Face;

        private byte _pad;
    }
}
