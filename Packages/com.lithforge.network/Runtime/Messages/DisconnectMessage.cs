using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Bidirectional disconnect notification. Sent on reliable pipeline before
    ///     closing the transport connection. Contains a reason byte.
    ///     Wire format: [Reason:1]
    /// </summary>
    public struct DisconnectMessage : INetworkMessage
    {
        /// <summary>
        /// Total payload size in bytes.
        /// </summary>
        public const int Size = 1;

        /// <summary>
        /// The reason for disconnection.
        /// </summary>
        public DisconnectReason Reason;

        /// <summary>
        /// Returns the MessageType for this message.
        /// </summary>
        public MessageType Type
        {
            get { return MessageType.Disconnect; }
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
            buffer[offset] = (byte)Reason;
            return Size;
        }

        /// <summary>
        /// Reads the message from the buffer. Returns a default message if the buffer is too small.
        /// </summary>
        public static DisconnectMessage Deserialize(byte[] buffer, int offset, int length)
        {
            DisconnectMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.Reason = (DisconnectReason)buffer[offset];
            return msg;
        }
    }
}
