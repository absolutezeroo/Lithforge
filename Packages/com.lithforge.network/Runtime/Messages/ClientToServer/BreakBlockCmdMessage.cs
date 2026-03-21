using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Client→Server block break command. Sent on reliable sequenced pipeline.
    ///     PlayerId is NOT on the wire — the server derives it from the ConnectionId.
    ///     Wire format: [SequenceId:2][PositionX:4][PositionY:4][PositionZ:4] = 14 bytes.
    /// </summary>
    public struct BreakBlockCmdMessage : INetworkMessage
    {
        /// <summary>
        /// Total payload size in bytes.
        /// </summary>
        public const int Size = 2 + 4 + 4 + 4; // 14 bytes

        /// <summary>
        /// Client-assigned sequence number for matching with the server acknowledgement.
        /// </summary>
        public ushort SequenceId;

        /// <summary>
        /// World-space X coordinate of the block to break.
        /// </summary>
        public int PositionX;

        /// <summary>
        /// World-space Y coordinate of the block to break.
        /// </summary>
        public int PositionY;

        /// <summary>
        /// World-space Z coordinate of the block to break.
        /// </summary>
        public int PositionZ;

        /// <summary>
        /// Returns the MessageType for this message.
        /// </summary>
        public MessageType Type
        {
            get { return MessageType.BreakBlockCmd; }
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
            int start = offset;
            MessageSerializer.WriteUShort(buffer, offset, SequenceId);
            offset += 2;
            MessageSerializer.WriteUInt(buffer, offset, (uint)PositionX);
            offset += 4;
            MessageSerializer.WriteUInt(buffer, offset, (uint)PositionY);
            offset += 4;
            MessageSerializer.WriteUInt(buffer, offset, (uint)PositionZ);
            offset += 4;
            return offset - start;
        }

        /// <summary>
        /// Reads the message from the buffer. Returns a default message if the buffer is too small.
        /// </summary>
        public static BreakBlockCmdMessage Deserialize(byte[] buffer, int offset, int length)
        {
            BreakBlockCmdMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.SequenceId = MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;
            msg.PositionX = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.PositionY = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.PositionZ = (int)MessageSerializer.ReadUInt(buffer, offset);
            return msg;
        }
    }
}
