using Lithforge.Voxel.Command;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     Carries one player's authoritative physics state from the main thread back
    ///     to the server thread after physics simulation.
    /// </summary>
    internal sealed class PhysicsTickResult
    {
        /// <summary>Network entity ID for the player this result belongs to.</summary>
        public NetworkEntityId PlayerId;

        /// <summary>Authoritative state after one tick of physics.</summary>
        public PlayerPhysicsState State;
    }
}
