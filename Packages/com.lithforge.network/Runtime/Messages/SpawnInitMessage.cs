using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server→Client spawn position delivery. Sent on reliable sequenced pipeline
    ///     immediately after handshake acceptance, before any ChunkData messages.
    ///     Lets the client know which chunk coordinate to gate readiness on before
    ///     chunks begin arriving. Also carries the client-ready radius so the client
    ///     knows how many chunks it needs before sending <see cref="ClientReadyMessage" />.
    ///     Wire format: [SpawnX:4][SpawnY:4][SpawnZ:4][ClientReadyRadius:1] = 13 bytes.
    /// </summary>
    public struct SpawnInitMessage : INetworkMessage
    {
        public const int Size = 4 + 4 + 4 + 1; // 13 bytes

        public float SpawnX;

        public float SpawnY;

        public float SpawnZ;

        public byte ClientReadyRadius;

        public MessageType Type
        {
            get
            {
                return MessageType.SpawnInit;
            }
        }

        public int GetSerializedSize()
        {
            return Size;
        }

        public int Serialize(byte[] buffer, int offset)
        {
            int start = offset;
            MessageSerializer.WriteFloat(buffer, offset, SpawnX);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, SpawnY);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, SpawnZ);
            offset += 4;
            buffer[offset] = ClientReadyRadius;
            offset += 1;

            return offset - start;
        }

        public static SpawnInitMessage Deserialize(byte[] buffer, int offset, int length)
        {
            SpawnInitMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.SpawnX = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.SpawnY = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.SpawnZ = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.ClientReadyRadius = buffer[offset];

            return msg;
        }
    }
}
