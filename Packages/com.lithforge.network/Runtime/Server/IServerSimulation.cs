using Lithforge.Voxel.Command;
using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Bridge interface between <see cref="ServerGameLoop" /> (network tier) and the gameplay
    ///     simulation (runtime tier). Implemented by Tier 3 to provide player physics,
    ///     world tick systems, and spawn management to the server loop.
    ///     The network package cannot reference Lithforge.Runtime directly, so this interface
    ///     allows the server loop to drive gameplay through dependency inversion.
    /// </summary>
    public interface IServerSimulation
    {
        /// <summary>
        ///     Creates a physics body for the given player at the spawn position.
        ///     Returns the initial <see cref="PlayerPhysicsState" /> after creation.
        /// </summary>
        public PlayerPhysicsState AddPlayer(NetworkEntityId playerId, float3 spawnPosition);

        /// <summary>Removes the physics body for the given player (on disconnect).</summary>
        public void RemovePlayer(NetworkEntityId playerId);

        /// <summary>
        ///     Validates a client-submitted position against the server's collision geometry
        ///     and movement rules. If valid, accepts it and updates the server-side physics body.
        ///     If invalid (speed, noclip, or flight violation), returns the last valid position
        ///     and sets <paramref name="needsTeleport" /> to true.
        /// </summary>
        public PlayerPhysicsState ValidateAndAcceptMove(
            NetworkEntityId playerId,
            float3 claimedPosition,
            float yaw,
            float pitch,
            byte flags,
            ref PlayerValidationState validationState,
            out bool needsTeleport);

        /// <summary>
        ///     Ticks all non-player simulation systems (block entities, time of day, etc.).
        /// </summary>
        public void TickWorldSystems(float tickDt);

        /// <summary>
        ///     Returns the current physics state for the given player.
        ///     Returns default if the player does not exist.
        /// </summary>
        public PlayerPhysicsState GetPlayerState(NetworkEntityId playerId);

        /// <summary>
        ///     Returns the current time of day (0-1 range) for inclusion in GameReadyMessage.
        /// </summary>
        public float GetTimeOfDay();

        /// <summary>
        ///     Accepts a client-authoritative position for the given player without
        ///     validation. Updates the cached state and teleports the server-side body
        ///     so chunk interest and reach checks use the correct position.
        ///     Used for the local peer in SP/Host mode.
        /// </summary>
        public void AcceptAuthoritativeState(NetworkEntityId playerId, PlayerPhysicsState state);
    }
}
