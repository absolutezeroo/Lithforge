namespace Lithforge.Network.Message
{
    /// <summary>
    ///     Interface for all network messages. Each message type knows how to
    ///     serialize itself to a byte array at a given offset.
    /// </summary>
    public interface INetworkMessage
    {
        /// <summary>
        ///     The message type identifier.
        /// </summary>
        public MessageType Type { get; }

        /// <summary>
        ///     Serializes the message payload (excluding header) into the buffer at the given offset.
        ///     Returns the number of bytes written.
        /// </summary>
        public int Serialize(byte[] buffer, int offset);

        /// <summary>
        ///     Returns the exact number of bytes this message's payload will occupy when serialized.
        /// </summary>
        public int GetSerializedSize();
    }
}
