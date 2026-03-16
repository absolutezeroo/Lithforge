using Unity.Mathematics;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    /// Manages per-player session state. In singleplayer there is exactly one player.
    /// In multiplayer, each connected player has a record keyed by player ID.
    /// Used by command processors and simulation services that need player position
    /// or state without direct access to Transform.
    /// </summary>
    public interface IPlayerManager
    {
        /// <summary>
        /// Gets the feet-position of the player with the given ID.
        /// Returns <see cref="float3.zero"/> if the player is not found.
        /// </summary>
        public float3 GetPosition(ushort playerId);

        /// <summary>
        /// Gets the yaw (Y rotation in degrees) of the player with the given ID.
        /// </summary>
        public float GetYaw(ushort playerId);

        /// <summary>
        /// Returns true if the player exists and their spawn sequence is complete.
        /// </summary>
        public bool IsReady(ushort playerId);

        /// <summary>
        /// Returns true if the given player is in fly mode.
        /// </summary>
        public bool IsFlying(ushort playerId);
    }
}
