namespace Lithforge.Network.Message
{
    /// <summary>
    /// Delegate type for message callbacks registered with MessageDispatcher.
    /// </summary>
    /// <param name="connectionId">The connection that sent this message.</param>
    /// <param name="data">The raw payload bytes (excluding header).</param>
    /// <param name="offset">Start offset of the payload in the data array.</param>
    /// <param name="length">Length of the payload in bytes.</param>
    public delegate void MessageHandler(ConnectionId connectionId, byte[] data, int offset, int length);
}
