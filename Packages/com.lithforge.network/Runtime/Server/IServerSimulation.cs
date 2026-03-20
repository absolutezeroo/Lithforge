using Lithforge.Voxel.Command;
using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    /// Bridge interface between <see cref="ServerGameLoop"/> (network tier) and the gameplay
    /// simulation (runtime tier). Implemented by Tier 3 to provide player physics,
    /// world tick systems, and spawn management to the server loop.
    /// The network package cannot reference Lithforge.Runtime directly, so this interface
    /// allows the server loop to drive gameplay through dependency inversion.
    /// </summary>
    public interface IServerSimulation
    {
        /// <summary>
        /// Creates a physics body for the given player at the spawn position.
        /// Returns the initial <see cref="PlayerPhysicsState"/> after creation.
        /// </summary>
        public PlayerPhysicsState AddPlayer(NetworkEntityId playerId, float3 spawnPosition);

        /// <summary>
        /// Removes the physics body for the given player (on disconnect).
        /// </summary>
        public void RemovePlayer(NetworkEntityId playerId);

        /// <summary>
        /// Applies a move input to the given player's physics simulation.
        /// Reconstructs an InputSnapshot from the flags and look direction,
        /// then runs one tick of physics. Returns the resulting authoritative state.
        /// </summary>
        public PlayerPhysicsState ApplyMoveInput(NetworkEntityId playerId, float yaw, float pitch, byte flags, float tickDt);

        /// <summary>
        /// Ticks all non-player simulation systems (block entities, time of day, liquids, etc.).
        /// </summary>
        public void TickWorldSystems(float tickDt);

        /// <summary>
        /// Returns the current physics state for the given player.
        /// Returns default if the player does not exist.
        /// </summary>
        public PlayerPhysicsState GetPlayerState(NetworkEntityId playerId);

        /// <summary>
        /// Returns the current time of day (0-1 range) for inclusion in GameReadyMessage.
        /// </summary>
        public float GetTimeOfDay();
    }
}
