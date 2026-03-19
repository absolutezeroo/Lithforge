namespace Lithforge.Network
{
    /// <summary>
    /// The role this process plays in a networked game session.
    /// </summary>
    public enum NetworkRole : byte
    {
        /// <summary>
        /// No networking active (offline or not yet initialized).
        /// </summary>
        None = 0,

        /// <summary>
        /// Headless server with no local player.
        /// </summary>
        DedicatedServer = 1,

        /// <summary>
        /// Remote client connected to a server.
        /// </summary>
        Client = 2,

        /// <summary>
        /// Server with a local player (listen server / single-player-as-server).
        /// </summary>
        Host = 3,
    }
}
