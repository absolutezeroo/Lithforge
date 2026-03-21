using Lithforge.Voxel.Command;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     Immutable snapshot of the local player's authoritative physics state,
    ///     written by the main thread after each client tick, read by the server thread
    ///     during ServerInputProcessor.ProcessTick for the local peer.
    ///     Reference swap via volatile field is atomic on all .NET platforms.
    /// </summary>
    public sealed class LocalPlayerStateSnapshot
    {
        /// <summary>The authoritative physics state captured from the client tick.</summary>
        public readonly PlayerPhysicsState State;

        /// <summary>Creates a new snapshot wrapping the given state.</summary>
        public LocalPlayerStateSnapshot(PlayerPhysicsState state)
        {
            State = state;
        }
    }
}
