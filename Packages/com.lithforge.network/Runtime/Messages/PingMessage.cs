using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    /// Bidirectional ping message for RTT measurement.
    /// Sent on unreliable sequenced pipeline every PingIntervalSeconds.
    /// Wire format: [Timestamp:4]
    /// </summary>
    public struct PingMessage : INetworkMessage
    {
        public const int Size = 4;

        public float Timestamp;

        public MessageType Type
        {
            get { return MessageType.Ping; }
        }

        public int GetSerializedSize()
        {
            return Size;
        }

        public int Serialize(byte[] buffer, int offset)
        {
            MessageSerializer.WriteFloat(buffer, offset, Timestamp);
            return Size;
        }

        public static PingMessage Deserialize(byte[] buffer, int offset, int length)
        {
            PingMessage msg = new PingMessage();

            if (length < Size)
            {
                return msg;
            }

            msg.Timestamp = MessageSerializer.ReadFloat(buffer, offset);
            return msg;
        }
    }
}
