using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Client→Server block placement command. Sent on reliable sequenced pipeline.
    ///     PlayerId is NOT on the wire — the server derives it from the ConnectionId.
    ///     Wire format: [SequenceId:2][PositionX:4][PositionY:4][PositionZ:4][StateId:2][Face:1] = 17 bytes.
    /// </summary>
    public struct PlaceBlockCmdMessage : INetworkMessage
    {
        public const int Size = 2 + 4 + 4 + 4 + 2 + 1; // 17 bytes

        public ushort SequenceId;
        public int PositionX;
        public int PositionY;
        public int PositionZ;
        public ushort BlockState;
        public byte Face;

        public MessageType Type
        {
            get { return MessageType.PlaceBlockCmd; }
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
            MessageSerializer.WriteUShort(buffer, offset, BlockState);
            offset += 2;
            buffer[offset] = Face;
            offset += 1;
            return offset - start;
        }

        public static PlaceBlockCmdMessage Deserialize(byte[] buffer, int offset, int length)
        {
            PlaceBlockCmdMessage msg = new();

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
            offset += 4;
            msg.BlockState = MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;
            msg.Face = buffer[offset];
            return msg;
        }
    }
}
