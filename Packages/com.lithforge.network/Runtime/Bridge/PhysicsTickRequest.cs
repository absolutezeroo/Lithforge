using Unity.Mathematics;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     Carries one player's physics input from the server thread to the main thread,
    ///     or signals player add/remove lifecycle events.
    /// </summary>
    internal sealed class PhysicsTickRequest
    {
        /// <summary>Discriminator for the type of physics request.</summary>
        public PhysicsRequestKind Kind;

        /// <summary>Network entity ID for the target player.</summary>
        public NetworkEntityId PlayerId;

        /// <summary>Yaw in degrees (for <see cref="PhysicsRequestKind.ApplyMove" />).</summary>
        public float Yaw;

        /// <summary>Pitch in degrees (for <see cref="PhysicsRequestKind.ApplyMove" />).</summary>
        public float Pitch;

        /// <summary>Input flags (for <see cref="PhysicsRequestKind.ApplyMove" />).</summary>
        public byte Flags;

        /// <summary>Tick delta time (for <see cref="PhysicsRequestKind.ApplyMove" />).</summary>
        public float TickDt;

        /// <summary>Spawn position (for <see cref="PhysicsRequestKind.AddPlayer" />).</summary>
        public float3 SpawnPosition;
    }
}
