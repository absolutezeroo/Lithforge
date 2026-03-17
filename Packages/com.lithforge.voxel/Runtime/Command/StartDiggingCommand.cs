using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Lithforge.Voxel.Command
{
    /// <summary>
    /// Blittable command for the start of block mining. Sent when the player
    /// begins holding left-click on a block. Paired with <see cref="BreakBlockCommand"/>
    /// (mining complete). The server uses the time between start and finish to
    /// validate break speed.
    /// 20 bytes, Burst-compatible.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct StartDiggingCommand
    {
        /// <summary>Server tick this command targets.</summary>
        public uint Tick;

        /// <summary>Per-player monotonic sequence number.</summary>
        public ushort SequenceId;

        /// <summary>Server-assigned player identifier.</summary>
        public ushort PlayerId;

        /// <summary>World-space coordinate of the block being mined.</summary>
        public int3 Position;
    }
}
