using Unity.Mathematics;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    /// Snapshot of a remote player's state at a specific server tick.
    /// Stored in <see cref="InterpolationBuffer{T}"/> for smooth rendering.
    /// </summary>
    public struct RemotePlayerSnapshot
    {
        public float3 Position;
        public float Yaw;
        public float Pitch;

        /// <summary>Flags byte from PlayerStateMessage (OnGround, IsFlying, etc.).</summary>
        public byte Flags;
    }
}
