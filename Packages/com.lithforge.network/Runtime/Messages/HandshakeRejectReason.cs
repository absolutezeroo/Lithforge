namespace Lithforge.Network.Messages
{
    /// <summary>
    /// Reason code sent in a HandshakeResponseMessage when the server rejects a client connection.
    /// </summary>
    public enum HandshakeRejectReason : byte
    {
        /// <summary>
        /// No rejection; the handshake was accepted.
        /// </summary>
        None = 0,

        /// <summary>
        /// Client and server protocol versions do not match.
        /// </summary>
        ProtocolMismatch = 1,

        /// <summary>
        /// Client and server content hashes differ (mismatched block/item definitions).
        /// </summary>
        ContentMismatch = 2,

        /// <summary>
        /// The server has reached its maximum player count.
        /// </summary>
        ServerFull = 3,

        /// <summary>
        /// The client is banned from this server.
        /// </summary>
        Banned = 4,
    }
}
