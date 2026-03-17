using System.Runtime.InteropServices;

namespace Lithforge.Voxel.Command
{
    /// <summary>
    /// Blittable command for inventory slot interactions.
    /// 16 bytes, Burst-compatible.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SlotClickCommand
    {
        /// <summary>Server tick this command targets.</summary>
        public uint Tick;

        /// <summary>
        /// Client's last-known inventory state ID. Server rejects the command
        /// with StateIdMismatch if this does not match the authoritative value,
        /// triggering a full resync.
        /// </summary>
        public uint StateId;

        /// <summary>Per-player monotonic sequence number for prediction reconciliation.</summary>
        public ushort SequenceId;

        /// <summary>Server-assigned player identifier.</summary>
        public ushort PlayerId;

        /// <summary>Container slot index. -1 = outside container (drop).</summary>
        public short SlotIndex;

        /// <summary>
        /// Click type: 0=Left, 1=Right, 2=ShiftLeft, 3=PaintDrag,
        /// 4=NumberKey, 5=OutputTake.
        /// </summary>
        public byte ClickType;

        /// <summary>
        /// Context byte: for NumberKey = hotbar index (0-8);
        /// for PaintDrag = 0=Begin, 1=Slot, 2=End.
        /// </summary>
        public byte Button;
    }
}
