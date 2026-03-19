using Unity.Mathematics;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    /// Snapshot of a remote player's state at a specific server tick.
    /// Stored in <see cref="InterpolationBuffer{T}"/> for smooth rendering.
    /// </summary>
    public struct RemotePlayerSnapshot
    {
        /// <summary>World-space feet position of the remote player.</summary>
        public float3 Position;

        /// <summary>Horizontal rotation (yaw) in degrees.</summary>
        public float Yaw;

        /// <summary>Vertical rotation (pitch) in degrees.</summary>
        public float Pitch;

        /// <summary>Flags byte from PlayerStateMessage (OnGround, IsFlying, etc.).</summary>
        public byte Flags;
    }
}
