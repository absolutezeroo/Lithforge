namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     Discriminator for <see cref="PhysicsTickRequest" /> to distinguish between
    ///     movement ticks and player lifecycle events.
    /// </summary>
    internal enum PhysicsRequestKind : byte
    {
        /// <summary>Apply one tick of movement input.</summary>
        ApplyMove = 0,

        /// <summary>Create a physics body for a new player.</summary>
        AddPlayer = 1,

        /// <summary>Remove the physics body for a disconnected player.</summary>
        RemovePlayer = 2,
    }
}
