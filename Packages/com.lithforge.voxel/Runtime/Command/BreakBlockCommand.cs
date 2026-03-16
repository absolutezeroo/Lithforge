using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Lithforge.Voxel.Command
{
    /// <summary>
    /// Blittable command for block breaking. Sent when mining completes.
    /// 20 bytes, Burst-compatible.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BreakBlockCommand
    {
        /// <summary>Server tick this command targets.</summary>
        public uint Tick;

        /// <summary>Per-player monotonic sequence number for prediction reconciliation.</summary>
        public ushort SequenceId;

        /// <summary>Server-assigned player identifier.</summary>
        public ushort PlayerId;

        /// <summary>World-space coordinate of the block to break.</summary>
        public int3 Position;
    }
}
