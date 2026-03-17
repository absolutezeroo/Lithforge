namespace Lithforge.Runtime.World
{
    /// <summary>
    /// Specifies the networking role for a game session.
    /// </summary>
    public enum NetworkRole
    {
        /// <summary>Singleplayer — no networking.</summary>
        Singleplayer,

        /// <summary>Host — runs both server and client (listen server).</summary>
        Host,

        /// <summary>Client — connects to a remote server.</summary>
        Client,

        /// <summary>Dedicated server — headless, no local player.</summary>
        DedicatedServer,
    }
}
