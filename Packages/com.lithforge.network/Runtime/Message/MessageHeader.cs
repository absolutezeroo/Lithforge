namespace Lithforge.Network.Message
{
    /// <summary>
    ///     4-byte message header: [type:1][payloadLength:2 LE][flags:1].
    ///     Precedes every message payload on the wire.
    /// </summary>
    public struct MessageHeader
    {
        /// <summary>
        /// Total size of the header in bytes.
        /// </summary>
        public const int Size = 4;

        /// <summary>
        /// The message type identifier byte.
        /// </summary>
        public MessageType Type;

        /// <summary>
        /// Length of the payload following this header, in bytes (little-endian on wire).
        /// </summary>
        public ushort PayloadLength;

        /// <summary>
        /// Reserved flags byte for future use (e.g., compression, fragmentation).
        /// </summary>
        public byte Flags;

        /// <summary>
        /// Writes a 4-byte header into the buffer at the given offset.
        /// </summary>
        public static void Write(byte[] buffer, int offset, MessageType type, ushort payloadLength, byte flags)
        {
            buffer[offset] = (byte)type;
            buffer[offset + 1] = (byte)(payloadLength & 0xFF);
            buffer[offset + 2] = (byte)(payloadLength >> 8 & 0xFF);
            buffer[offset + 3] = flags;
        }

        /// <summary>
        /// Reads a 4-byte header from the buffer at the given offset.
        /// </summary>
        public static MessageHeader Read(byte[] buffer, int offset)
        {
            MessageHeader header = new()
            {
                Type = (MessageType)buffer[offset], PayloadLength = (ushort)(buffer[offset + 1] | buffer[offset + 2] << 8), Flags = buffer[offset + 3],
            };
            return header;
        }
    }
}
