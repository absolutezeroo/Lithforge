using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Per-player server-side movement validation state. Embedded by value in
    ///     <see cref="PlayerInterestState" /> — no heap allocation. Tracks violation
    ///     level (VL), last accepted position, airborne tick count, and teleport
    ///     handshake state for the client-authoritative movement model.
    /// </summary>
    public struct PlayerValidationState
    {
        /// <summary>Accumulated violation level. Decays each clean tick.</summary>
        public float ViolationLevel;

        /// <summary>Last position the server accepted as valid. Reference for next tick's speed check.</summary>
        public float3 LastAcceptedPosition;

        /// <summary>True while the server is awaiting a TeleportConfirm from the client.</summary>
        public bool AwaitingTeleportConfirm;

        /// <summary>Unique ID sent in the most recent ServerTeleportMessage.</summary>
        public ushort PendingTeleportId;

        /// <summary>Server tick at which the current teleport was sent. Used for 20-tick timeout.</summary>
        public uint TeleportSentTick;

        /// <summary>Number of consecutive ticks the player has been airborne without ground contact.</summary>
        public uint AirborneTicks;

        /// <summary>Number of ticks remaining in the post-teleport grace period (checks suppressed).</summary>
        public uint GraceTicks;

        /// <summary>Whether flight mode is currently active for this player (toggled by FlyToggle input).</summary>
        public bool IsFlightModeActive;
    }
}
