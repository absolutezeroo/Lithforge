using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Client→Server teleport acknowledgement. Sent on reliable sequenced pipeline
    ///     immediately after applying a <see cref="ServerTeleportMessage" />. Until the
    ///     server receives this, it discards all MoveInput from this player.
    ///     Wire format: [TeleportId:2] = 2 bytes.
    /// </summary>
    public struct TeleportConfirmMessage : INetworkMessage
    {
        /// <summary>Total payload size in bytes.</summary>
        public const int Size = 2;

        /// <summary>The TeleportId echoed from the corresponding ServerTeleportMessage.</summary>
        public ushort TeleportId;

        /// <summary>Returns the MessageType for this message.</summary>
        public MessageType Type
        {
            get { return MessageType.TeleportConfirm; }
        }

        /// <summary>Returns the fixed payload size in bytes.</summary>
        public int GetSerializedSize()
        {
            return Size;
        }

        /// <summary>Writes the message payload into the buffer at the given offset.</summary>
        public int Serialize(byte[] buffer, int offset)
        {
            int start = offset;
            MessageSerializer.WriteUShort(buffer, offset, TeleportId);
            offset += 2;
            return offset - start;
        }

        /// <summary>Reads the message from the buffer. Returns a default message if too small.</summary>
        public static TeleportConfirmMessage Deserialize(byte[] buffer, int offset, int length)
        {
            TeleportConfirmMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.TeleportId = MessageSerializer.ReadUShort(buffer, offset);
            return msg;
        }
    }
}
