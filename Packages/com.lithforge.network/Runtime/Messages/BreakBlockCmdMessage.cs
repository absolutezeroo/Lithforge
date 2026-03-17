using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    /// Client→Server block break command. Sent on reliable sequenced pipeline.
    /// PlayerId is NOT on the wire — the server derives it from the ConnectionId.
    /// Wire format: [SequenceId:2][PositionX:4][PositionY:4][PositionZ:4] = 14 bytes.
    /// </summary>
    public struct BreakBlockCmdMessage : INetworkMessage
    {
        public const int Size = 2 + 4 + 4 + 4; // 14 bytes

        public ushort SequenceId;
        public int PositionX;
        public int PositionY;
        public int PositionZ;

        public MessageType Type
        {
            get { return MessageType.BreakBlockCmd; }
        }

        public int GetSerializedSize()
        {
            return Size;
        }

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

        public static BreakBlockCmdMessage Deserialize(byte[] buffer, int offset, int length)
        {
            BreakBlockCmdMessage msg = new BreakBlockCmdMessage();

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
