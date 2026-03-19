using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Client→Server signal that the client has received enough chunks within its
    ///     configured ready radius around the spawn position to begin playing.
    ///     The server responds by transitioning the peer to Playing and sending GameReady.
    ///     Wire format: [ReadyRadius:1] = 1 byte.
    /// </summary>
    public struct ClientReadyMessage : INetworkMessage
    {
        /// <summary>
        /// Total payload size in bytes.
        /// </summary>
        public const int Size = 1;

        /// <summary>The ready radius the client used to determine readiness (for server logging).</summary>
        public byte ReadyRadius;

        /// <summary>
        /// Returns the MessageType for this message.
        /// </summary>
        public MessageType Type
        {
            get
            {
                return MessageType.ClientReady;
            }
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
            buffer[offset] = ReadyRadius;

            return Size;
        }

        /// <summary>
        /// Reads the message from the buffer. Returns a default message if the buffer is too small.
        /// </summary>
        public static ClientReadyMessage Deserialize(byte[] buffer, int offset, int length)
        {
            ClientReadyMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.ReadyRadius = buffer[offset];

            return msg;
        }
    }
}
