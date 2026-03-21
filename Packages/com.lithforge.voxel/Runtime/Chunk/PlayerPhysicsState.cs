using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Lithforge.Voxel.Command
{
    /// <summary>
    /// Snapshot of a single player's physics state.
    /// Blittable, Burst-compatible. Suitable for NativeArray storage.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PlayerPhysicsState
    {
        /// <summary>Feet position in world space.</summary>
        public float3 Position;

        /// <summary>Current velocity vector.</summary>
        public float3 Velocity;

        /// <summary>Player yaw in degrees.</summary>
        public float Yaw;

        /// <summary>Camera pitch in degrees.</summary>
        public float Pitch;

        /// <summary>
        /// Bit 0 = OnGround, Bit 1 = IsFlying, Bit 2 = IsNoclip.
        /// </summary>
        public byte Flags;

        private byte _pad0;
        private byte _pad1;
        private byte _pad2;

        /// <summary>Bit 0 of <see cref="Flags"/>.</summary>
        public bool OnGround
        {
            get { return (Flags & 1) != 0; }
        }

        /// <summary>Bit 1 of <see cref="Flags"/>.</summary>
        public bool IsFlying
        {
            get { return (Flags & 2) != 0; }
        }

        /// <summary>Bit 2 of <see cref="Flags"/>.</summary>
        public bool IsNoclip
        {
            get { return (Flags & 4) != 0; }
        }
    }
}
