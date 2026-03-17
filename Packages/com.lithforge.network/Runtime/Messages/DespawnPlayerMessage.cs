using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    /// Server→Client despawn notification for a remote player leaving the observer's
    /// interest range or disconnecting. Sent on reliable sequenced pipeline.
    /// Wire format: [PlayerId:2] = 2 bytes.
    /// </summary>
    public struct DespawnPlayerMessage : INetworkMessage
    {
        public const int Size = 2;

        public ushort PlayerId;

        public MessageType Type
        {
            get { return MessageType.DespawnPlayer; }
        }

        public int GetSerializedSize()
        {
            return Size;
        }

        public int Serialize(byte[] buffer, int offset)
        {
            MessageSerializer.WriteUShort(buffer, offset, PlayerId);
            return Size;
        }

        public static DespawnPlayerMessage Deserialize(byte[] buffer, int offset, int length)
        {
            DespawnPlayerMessage msg = new DespawnPlayerMessage();

            if (length < Size)
            {
                return msg;
            }

            msg.PlayerId = MessageSerializer.ReadUShort(buffer, offset);
            return msg;
        }
    }
}
