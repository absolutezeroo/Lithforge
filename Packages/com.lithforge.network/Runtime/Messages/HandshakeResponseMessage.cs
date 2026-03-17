using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    /// Server→Client handshake response. Sent on reliable pipeline after validating
    /// the client's HandshakeRequest.
    /// Wire format: [Accepted:1][RejectReason:1][PlayerId:2][ServerTick:4][WorldSeed:8]
    /// </summary>
    public struct HandshakeResponseMessage : INetworkMessage
    {
        public const int Size = 1 + 1 + 2 + 4 + 8; // 16 bytes

        public bool Accepted;
        public HandshakeRejectReason RejectReason;
        public ushort PlayerId;
        public uint ServerTick;
        public ulong WorldSeed;

        public MessageType Type
        {
            get { return MessageType.HandshakeResponse; }
        }

        public int GetSerializedSize()
        {
            return Size;
        }

        public int Serialize(byte[] buffer, int offset)
        {
            buffer[offset] = Accepted ? (byte)1 : (byte)0;
            buffer[offset + 1] = (byte)RejectReason;
            MessageSerializer.WriteUShort(buffer, offset + 2, PlayerId);
            MessageSerializer.WriteUInt(buffer, offset + 4, ServerTick);
            MessageSerializer.WriteULong(buffer, offset + 8, WorldSeed);
            return Size;
        }

        public static HandshakeResponseMessage Deserialize(byte[] buffer, int offset, int length)
        {
            HandshakeResponseMessage msg = new HandshakeResponseMessage();

            if (length < Size)
            {
                return msg;
            }

            msg.Accepted = buffer[offset] != 0;
            msg.RejectReason = (HandshakeRejectReason)buffer[offset + 1];
            msg.PlayerId = MessageSerializer.ReadUShort(buffer, offset + 2);
            msg.ServerTick = MessageSerializer.ReadUInt(buffer, offset + 4);
            msg.WorldSeed = MessageSerializer.ReadULong(buffer, offset + 8);
            return msg;
        }
    }
}
