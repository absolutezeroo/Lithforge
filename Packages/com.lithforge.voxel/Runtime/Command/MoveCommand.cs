using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Lithforge.Voxel.Command
{
    /// <summary>
    /// Blittable command for player movement. Sent every tick with the client-predicted
    /// position and the raw input flags that produced it.
    /// 32 bytes, Burst-compatible.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MoveCommand
    {
        /// <summary>Server tick this command targets.</summary>
        public uint Tick;

        /// <summary>Per-player monotonic sequence number for prediction reconciliation.</summary>
        public ushort SequenceId;

        /// <summary>Server-assigned player identifier.</summary>
        public ushort PlayerId;

        /// <summary>Client-predicted feet position at end of this tick.</summary>
        public float3 Position;

        /// <summary>Player look direction as (yaw, pitch) in degrees.</summary>
        public float2 LookDir;

        /// <summary>Bit-packed continuous input flags. See <see cref="InputFlags"/>.</summary>
        public byte Flags;

        private byte _pad0;
        private byte _pad1;
        private byte _pad2;
    }

    /// <summary>
    /// Bit positions for <see cref="MoveCommand.Flags"/>.
    /// </summary>
    public static class InputFlags
    {
        public const byte MoveForward = 1 << 0;
        public const byte MoveBack = 1 << 1;
        public const byte MoveLeft = 1 << 2;
        public const byte MoveRight = 1 << 3;
        public const byte Sprint = 1 << 4;
        public const byte Jump = 1 << 5;
        public const byte Sneak = 1 << 6;
    }
}
