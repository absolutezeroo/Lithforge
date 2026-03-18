using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server→Client loading progress update. Sent periodically during the Loading
    ///     phase on unreliable sequenced pipeline so the remote client can display
    ///     accurate chunk readiness progress on the loading screen.
    ///     Wire format: [ReadyChunks:2][TotalChunks:2] = 4 bytes.
    /// </summary>
    public struct LoadingProgressMessage : INetworkMessage
    {
        public const int Size = 4;

        public ushort ReadyChunks;

        public ushort TotalChunks;

        public MessageType Type
        {
            get { return MessageType.LoadingProgress; }
        }

        public int GetSerializedSize()
        {
            return Size;
        }

        public int Serialize(byte[] buffer, int offset)
        {
            int start = offset;
            MessageSerializer.WriteUShort(buffer, offset, ReadyChunks);
            offset += 2;
            MessageSerializer.WriteUShort(buffer, offset, TotalChunks);
            offset += 2;

            return offset - start;
        }

        public static LoadingProgressMessage Deserialize(byte[] buffer, int offset, int length)
        {
            LoadingProgressMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.ReadyChunks = MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;
            msg.TotalChunks = MessageSerializer.ReadUShort(buffer, offset);

            return msg;
        }
    }
}
