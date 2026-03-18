using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    /// Server→Client block command acknowledgement. Sent on reliable sequenced pipeline.
    /// Confirms or rejects a client's optimistic block prediction. If rejected,
    /// <see cref="CorrectedState"/> contains the server's actual block state so the
    /// client can revert its local prediction.
    /// Wire format: [SequenceId:2][Accepted:1][PositionX:4][PositionY:4][PositionZ:4][CorrectedState:2] = 17 bytes.
    /// </summary>
    public struct AcknowledgeBlockChangeMessage : INetworkMessage
    {
        public const int Size = 2 + 1 + 4 + 4 + 4 + 2; // 17 bytes

        public ushort SequenceId;
        public byte Accepted;
        public int PositionX;
        public int PositionY;
        public int PositionZ;
        public ushort CorrectedState;

        public MessageType Type
        {
            get { return MessageType.AcknowledgeBlockChange; }
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
            buffer[offset] = Accepted;
            offset += 1;
            MessageSerializer.WriteUInt(buffer, offset, (uint)PositionX);
            offset += 4;
            MessageSerializer.WriteUInt(buffer, offset, (uint)PositionY);
            offset += 4;
            MessageSerializer.WriteUInt(buffer, offset, (uint)PositionZ);
            offset += 4;
            MessageSerializer.WriteUShort(buffer, offset, CorrectedState);
            offset += 2;
            return offset - start;
        }

        public static AcknowledgeBlockChangeMessage Deserialize(byte[] buffer, int offset, int length)
        {
            AcknowledgeBlockChangeMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.SequenceId = MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;
            msg.Accepted = buffer[offset];
            offset += 1;
            msg.PositionX = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.PositionY = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.PositionZ = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.CorrectedState = MessageSerializer.ReadUShort(buffer, offset);
            return msg;
        }
    }
}
