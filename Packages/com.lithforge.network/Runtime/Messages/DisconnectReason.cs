namespace Lithforge.Network.Messages
{
    /// <summary>
    /// Reason code sent with a DisconnectMessage to indicate why the connection was closed.
    /// </summary>
    public enum DisconnectReason : byte
    {
        /// <summary>
        /// The peer disconnected normally (e.g., player quit).
        /// </summary>
        Graceful = 0,

        /// <summary>
        /// The connection timed out due to lack of response.
        /// </summary>
        Timeout = 1,

        /// <summary>
        /// The player was kicked by the server operator.
        /// </summary>
        Kicked = 2,

        /// <summary>
        /// The server is shutting down.
        /// </summary>
        ServerShutdown = 3,
    }
}
