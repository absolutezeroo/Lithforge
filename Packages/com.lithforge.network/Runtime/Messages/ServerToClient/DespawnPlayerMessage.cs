using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server→Client despawn notification for a remote player leaving the observer's
    ///     interest range or disconnecting. Sent on reliable sequenced pipeline.
    ///     Wire format: [PlayerId:2] = 2 bytes.
    /// </summary>
    public struct DespawnPlayerMessage : INetworkMessage
    {
        /// <summary>
        /// Total payload size in bytes.
        /// </summary>
        public const int Size = 2;

        /// <summary>
        /// Network-assigned player identifier to despawn.
        /// </summary>
        public ushort PlayerId;

        /// <summary>
        /// Returns the MessageType for this message.
        /// </summary>
        public MessageType Type
        {
            get { return MessageType.DespawnPlayer; }
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
            MessageSerializer.WriteUShort(buffer, offset, PlayerId);
            return Size;
        }

        /// <summary>
        /// Reads the message from the buffer. Returns a default message if the buffer is too small.
        /// </summary>
        public static DespawnPlayerMessage Deserialize(byte[] buffer, int offset, int length)
        {
            DespawnPlayerMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.PlayerId = MessageSerializer.ReadUShort(buffer, offset);
            return msg;
        }
    }
}
