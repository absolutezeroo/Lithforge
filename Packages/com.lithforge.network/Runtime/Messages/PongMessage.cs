using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    /// Bidirectional pong response for RTT measurement.
    /// Echoes the original timestamp plus the current server tick.
    /// Wire format: [EchoTimestamp:4][ServerTick:4]
    /// </summary>
    public struct PongMessage : INetworkMessage
    {
        public const int Size = 8;

        public float EchoTimestamp;
        public uint ServerTick;

        public MessageType Type
        {
            get { return MessageType.Pong; }
        }

        public int GetSerializedSize()
        {
            return Size;
        }

        public int Serialize(byte[] buffer, int offset)
        {
            MessageSerializer.WriteFloat(buffer, offset, EchoTimestamp);
            MessageSerializer.WriteUInt(buffer, offset + 4, ServerTick);
            return Size;
        }

        public static PongMessage Deserialize(byte[] buffer, int offset, int length)
        {
            PongMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.EchoTimestamp = MessageSerializer.ReadFloat(buffer, offset);
            msg.ServerTick = MessageSerializer.ReadUInt(buffer, offset + 4);
            return msg;
        }
    }
}
