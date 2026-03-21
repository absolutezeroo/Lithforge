using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Bidirectional pong response for RTT measurement.
    ///     Echoes the original timestamp plus the current server tick.
    ///     Wire format: [EchoTimestamp:4][ServerTick:4]
    /// </summary>
    public struct PongMessage : INetworkMessage
    {
        /// <summary>
        /// Total payload size in bytes.
        /// </summary>
        public const int Size = 8;

        /// <summary>
        /// The original ping timestamp echoed back for RTT calculation.
        /// </summary>
        public float EchoTimestamp;

        /// <summary>
        /// The server's current tick at the time the pong was sent.
        /// </summary>
        public uint ServerTick;

        /// <summary>
        /// Returns the MessageType for this message.
        /// </summary>
        public MessageType Type
        {
            get { return MessageType.Pong; }
        }

        /// <summary>
        /// Returns the fixed payload size in bytes.
        /// </summary>
        public int GetSerializedSize()
        {
            return Size;
        }

        /// <summary>
        /// Writes the message payload into the buffer at the given offset.
        /// </summary>
        public int Serialize(byte[] buffer, int offset)
        {
            MessageSerializer.WriteFloat(buffer, offset, EchoTimestamp);
            MessageSerializer.WriteUInt(buffer, offset + 4, ServerTick);
            return Size;
        }

        /// <summary>
        /// Reads the message from the buffer. Returns a default message if the buffer is too small.
        /// </summary>
        public static PongMessage Deserialize(byte[] buffer, int offset, int length)
        {
            PongMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.EchoTimestamp = MessageSerializer.ReadFloat(buffer, offset);
            msg.ServerTick = MessageSerializer.ReadUInt(buffer, offset + 4);
            return msg;
        }
    }
}
