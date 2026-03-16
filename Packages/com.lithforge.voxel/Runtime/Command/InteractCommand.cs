using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Lithforge.Voxel.Command
{
    /// <summary>
    /// Blittable command for interacting with a block entity (open container, use item).
    /// 24 bytes, Burst-compatible.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct InteractCommand
    {
        /// <summary>Server tick this command targets.</summary>
        public uint Tick;

        /// <summary>Per-player monotonic sequence number for prediction reconciliation.</summary>
        public ushort SequenceId;

        /// <summary>Server-assigned player identifier.</summary>
        public ushort PlayerId;

        /// <summary>World-space coordinate of the targeted block entity.</summary>
        public int3 Position;

        /// <summary>Type of interaction. 0 = OpenContainer, 1 = UseItem.</summary>
        public byte InteractionType;

        private byte _pad0;
        private byte _pad1;
        private byte _pad2;
    }
}
