namespace Lithforge.Network.Message
{
    /// <summary>
    ///     4-byte message header: [type:1][payloadLength:2 LE][flags:1].
    ///     Precedes every message payload on the wire.
    /// </summary>
    public struct MessageHeader
    {
        public const int Size = 4;

        public MessageType Type;
        public ushort PayloadLength;
        public byte Flags;

        public static void Write(byte[] buffer, int offset, MessageType type, ushort payloadLength, byte flags)
        {
            buffer[offset] = (byte)type;
            buffer[offset + 1] = (byte)(payloadLength & 0xFF);
            buffer[offset + 2] = (byte)(payloadLength >> 8 & 0xFF);
            buffer[offset + 3] = flags;
        }

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
