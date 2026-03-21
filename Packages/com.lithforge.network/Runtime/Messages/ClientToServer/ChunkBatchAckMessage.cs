using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Client→Server acknowledgment for received chunks. The client sends this
    ///     periodically after processing a batch of <see cref="ChunkDataMessage" /> packets.
    ///     The server decrements the per-peer in-flight count, allowing further streaming.
    ///     Wire format: [Count:2] = 2 bytes.
    /// </summary>
    public struct ChunkBatchAckMessage : INetworkMessage
    {
        /// <summary>Wire size in bytes.</summary>
        public const int Size = 2;

        /// <summary>Number of chunks the client has finished processing since the last ACK.</summary>
        public ushort Count;

        /// <inheritdoc />
        public MessageType Type
        {
            get { return MessageType.ChunkBatchAck; }
        }

        /// <inheritdoc />
        public int GetSerializedSize()
        {
            return Size;
        }

        /// <inheritdoc />
        public int Serialize(byte[] buffer, int offset)
        {
            int start = offset;
            MessageSerializer.WriteUShort(buffer, offset, Count);
            offset += 2;

            return offset - start;
        }

        /// <summary>Deserializes a ChunkBatchAckMessage from the given buffer.</summary>
        public static ChunkBatchAckMessage Deserialize(byte[] buffer, int offset, int length)
        {
            ChunkBatchAckMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.Count = MessageSerializer.ReadUShort(buffer, offset);

            return msg;
        }
    }
}
