using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server→Client chunk unload notification. Sent on reliable sequenced pipeline.
    ///     Tells the client to release the specified chunk and stop expecting block updates for it.
    ///     Wire format: [ChunkX:4][ChunkY:4][ChunkZ:4] = 12 bytes.
    /// </summary>
    public struct ChunkUnloadMessage : INetworkMessage
    {
        public const int Size = 4 + 4 + 4; // 12 bytes

        public int ChunkX;
        public int ChunkY;
        public int ChunkZ;

        public MessageType Type
        {
            get { return MessageType.ChunkUnload; }
        }

        public int GetSerializedSize()
        {
            return Size;
        }

        public int Serialize(byte[] buffer, int offset)
        {
            int start = offset;
            MessageSerializer.WriteUInt(buffer, offset, (uint)ChunkX);
            offset += 4;
            MessageSerializer.WriteUInt(buffer, offset, (uint)ChunkY);
            offset += 4;
            MessageSerializer.WriteUInt(buffer, offset, (uint)ChunkZ);
            offset += 4;
            return offset - start;
        }

        public static ChunkUnloadMessage Deserialize(byte[] buffer, int offset, int length)
        {
            ChunkUnloadMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.ChunkX = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.ChunkY = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.ChunkZ = (int)MessageSerializer.ReadUInt(buffer, offset);
            return msg;
        }
    }
}
