using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    /// Client→Server movement input. Sent every tick on unreliable sequenced pipeline.
    /// The server reconstructs InputSnapshot from the flags and look direction,
    /// then runs player physics authoritatively.
    /// Wire format: [SequenceId:2][Yaw:4][Pitch:4][Flags:1] = 11 bytes.
    /// PlayerId is NOT on the wire — the server derives it from the ConnectionId.
    /// </summary>
    public struct MoveInputMessage : INetworkMessage
    {
        public const int Size = 2 + 4 + 4 + 1; // 11 bytes

        public ushort SequenceId;
        public float Yaw;
        public float Pitch;
        public byte Flags;

        public MessageType Type
        {
            get { return MessageType.MoveInput; }
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
            MessageSerializer.WriteFloat(buffer, offset, Yaw);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, Pitch);
            offset += 4;
            buffer[offset] = Flags;
            offset += 1;
            return offset - start;
        }

        public static MoveInputMessage Deserialize(byte[] buffer, int offset, int length)
        {
            MoveInputMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.SequenceId = MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;
            msg.Yaw = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.Pitch = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.Flags = buffer[offset];
            return msg;
        }
    }
}
