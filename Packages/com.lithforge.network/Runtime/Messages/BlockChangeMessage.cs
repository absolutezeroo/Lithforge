using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server→Client single block change notification. Sent on reliable sequenced pipeline.
    ///     Used when exactly one block changed in a chunk this tick (for 2+ changes,
    ///     use <see cref="MultiBlockChangeMessage" />).
    ///     Wire format: [PositionX:4][PositionY:4][PositionZ:4][StateId:2] = 14 bytes.
    /// </summary>
    public struct BlockChangeMessage : INetworkMessage
    {
        public const int Size = 4 + 4 + 4 + 2; // 14 bytes

        public int PositionX;
        public int PositionY;
        public int PositionZ;
        public ushort NewState;

        public MessageType Type
        {
            get { return MessageType.BlockChange; }
        }

        public int GetSerializedSize()
        {
            return Size;
        }

        public int Serialize(byte[] buffer, int offset)
        {
            int start = offset;
            MessageSerializer.WriteUInt(buffer, offset, (uint)PositionX);
            offset += 4;
            MessageSerializer.WriteUInt(buffer, offset, (uint)PositionY);
            offset += 4;
            MessageSerializer.WriteUInt(buffer, offset, (uint)PositionZ);
            offset += 4;
            MessageSerializer.WriteUShort(buffer, offset, NewState);
            offset += 2;
            return offset - start;
        }

        public static BlockChangeMessage Deserialize(byte[] buffer, int offset, int length)
        {
            BlockChangeMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.PositionX = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.PositionY = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.PositionZ = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.NewState = MessageSerializer.ReadUShort(buffer, offset);
            return msg;
        }
    }
}
