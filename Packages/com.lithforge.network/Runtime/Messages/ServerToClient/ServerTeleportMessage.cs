using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server→Client forced teleport. Sent when the server's movement validation
    ///     pipeline detects violations exceeding the teleport threshold. The client must
    ///     apply this position and reply with <see cref="TeleportConfirmMessage" /> before
    ///     the server resumes accepting movement input.
    ///     Wire format: [TeleportId:2][PositionX:4][PositionY:4][PositionZ:4] = 14 bytes.
    /// </summary>
    public struct ServerTeleportMessage : INetworkMessage
    {
        /// <summary>Total payload size in bytes.</summary>
        public const int Size = 2 + 4 + 4 + 4; // 14 bytes

        /// <summary>Monotonically increasing teleport sequence number per player.</summary>
        public ushort TeleportId;

        /// <summary>Corrected X position the client must snap to.</summary>
        public float PositionX;

        /// <summary>Corrected Y position the client must snap to.</summary>
        public float PositionY;

        /// <summary>Corrected Z position the client must snap to.</summary>
        public float PositionZ;

        /// <summary>Returns the MessageType for this message.</summary>
        public MessageType Type
        {
            get { return MessageType.ServerTeleport; }
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
            MessageSerializer.WriteFloat(buffer, offset, PositionX);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, PositionY);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, PositionZ);
            offset += 4;
            return offset - start;
        }

        /// <summary>Reads the message from the buffer. Returns a default message if too small.</summary>
        public static ServerTeleportMessage Deserialize(byte[] buffer, int offset, int length)
        {
            ServerTeleportMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.TeleportId = MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;
            msg.PositionX = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.PositionY = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.PositionZ = MessageSerializer.ReadFloat(buffer, offset);
            return msg;
        }
    }
}
