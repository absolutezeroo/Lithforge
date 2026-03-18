using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    /// Bidirectional disconnect notification. Sent on reliable pipeline before
    /// closing the transport connection. Contains a reason byte.
    /// Wire format: [Reason:1]
    /// </summary>
    public struct DisconnectMessage : INetworkMessage
    {
        public const int Size = 1;

        public DisconnectReason Reason;

        public MessageType Type
        {
            get { return MessageType.Disconnect; }
        }

        public int GetSerializedSize()
        {
            return Size;
        }

        public int Serialize(byte[] buffer, int offset)
        {
            buffer[offset] = (byte)Reason;
            return Size;
        }

        public static DisconnectMessage Deserialize(byte[] buffer, int offset, int length)
        {
            DisconnectMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.Reason = (DisconnectReason)buffer[offset];
            return msg;
        }
    }
}
