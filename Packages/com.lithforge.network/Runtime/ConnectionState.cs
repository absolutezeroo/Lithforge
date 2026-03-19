namespace Lithforge.Network
{
    /// <summary>
    ///     Connection lifecycle states. The full happy-path sequence is:
    ///     Disconnected → Connecting → Handshaking → Authenticating → Configuring → Loading → Playing.
    ///     Reconnecting is entered from Disconnected when a session token allows rejoin.
    ///     Any state may transition to Disconnected. Playing may re-enter Configuring for
    ///     hot-reload of server registries without disconnecting.
    /// </summary>
    public enum ConnectionState : byte
    {
        /// <summary>Not connected. Initial and terminal state.</summary>
        Disconnected = 0,

        /// <summary>Transport-level connection in progress.</summary>
        Connecting = 1,

        /// <summary>Protocol version and content hash exchange.</summary>
        Handshaking = 2,

        /// <summary>Identity verification (content hash validated, player name accepted).</summary>
        Authenticating = 3,

        /// <summary>Registry synchronization and mod negotiation. Re-enterable from Playing.</summary>
        Configuring = 4,

        /// <summary>Initial chunk streaming phase.</summary>
        Loading = 5,

        /// <summary>Fully active gameplay state.</summary>
        Playing = 6,

        /// <summary>Graceful disconnect in progress.</summary>
        Disconnecting = 7,

        /// <summary>Attempting to rejoin with a valid session token after a disconnect.</summary>
        Reconnecting = 8,
    }
}
