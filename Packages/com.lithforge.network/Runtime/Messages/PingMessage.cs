using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Bidirectional ping message for RTT measurement.
    ///     Sent on unreliable sequenced pipeline every PingIntervalSeconds.
    ///     Wire format: [Timestamp:4]
    /// </summary>
    public struct PingMessage : INetworkMessage
    {
        /// <summary>
        /// Total payload size in bytes.
        /// </summary>
        public const int Size = 4;

        /// <summary>
        /// Sender's local time when the ping was sent, echoed back in the Pong for RTT calculation.
        /// </summary>
        public float Timestamp;

        /// <summary>
        /// Returns the MessageType for this message.
        /// </summary>
        public MessageType Type
        {
            get { return MessageType.Ping; }
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
            MessageSerializer.WriteFloat(buffer, offset, Timestamp);
            return Size;
        }

        /// <summary>
        /// Reads the message from the buffer. Returns a default message if the buffer is too small.
        /// </summary>
        public static PingMessage Deserialize(byte[] buffer, int offset, int length)
        {
            PingMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.Timestamp = MessageSerializer.ReadFloat(buffer, offset);
            return msg;
        }
    }
}
