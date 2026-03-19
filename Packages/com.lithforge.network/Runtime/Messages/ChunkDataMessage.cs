using System;

using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server→Client full chunk data. Sent on fragmented reliable pipeline.
    ///     Contains chunk coordinate and the serialized chunk payload from
    ///     <see cref="Lithforge.Voxel.Network.ChunkNetSerializer.SerializeFullChunk" />.
    ///     Wire format: [ChunkX:4][ChunkY:4][ChunkZ:4][PayloadLength:4][Payload:N] = 16 + N bytes.
    /// </summary>
    public struct ChunkDataMessage : INetworkMessage
    {
        /// <summary>
        /// Size of the fixed header portion in bytes (coordinates + payload length).
        /// </summary>
        public const int HeaderSize = 4 + 4 + 4 + 4; // 16 bytes

        /// <summary>
        /// Chunk coordinate X.
        /// </summary>
        public int ChunkX;

        /// <summary>
        /// Chunk coordinate Y.
        /// </summary>
        public int ChunkY;

        /// <summary>
        /// Chunk coordinate Z.
        /// </summary>
        public int ChunkZ;

        /// <summary>
        ///     Serialized chunk data from ChunkNetSerializer.SerializeFullChunk.
        /// </summary>
        public byte[] Payload;

        /// <summary>
        /// Returns the MessageType for this message.
        /// </summary>
        public MessageType Type
        {
            get { return MessageType.ChunkData; }
        }

        /// <summary>
        /// Returns the total payload size including the header and variable-length chunk data.
        /// </summary>
        public int GetSerializedSize()
        {
            int payloadLen = Payload?.Length ?? 0;

            return HeaderSize + payloadLen;
        }

        /// <summary>
        /// Writes the message payload into the buffer at the given offset.
        /// </summary>
        public int Serialize(byte[] buffer, int offset)
        {
            int start = offset;
            MessageSerializer.WriteUInt(buffer, offset, (uint)ChunkX);
            offset += 4;
            MessageSerializer.WriteUInt(buffer, offset, (uint)ChunkY);
            offset += 4;
            MessageSerializer.WriteUInt(buffer, offset, (uint)ChunkZ);
            offset += 4;

            int payloadLen = Payload?.Length ?? 0;
            MessageSerializer.WriteUInt(buffer, offset, (uint)payloadLen);
            offset += 4;

            if (payloadLen > 0)
            {
                Buffer.BlockCopy(Payload, 0, buffer, offset, payloadLen);
                offset += payloadLen;
            }

            return offset - start;
        }

        /// <summary>
        /// Reads the message from the buffer. Returns a default message if the buffer is too small.
        /// </summary>
        public static ChunkDataMessage Deserialize(byte[] buffer, int offset, int length)
        {
            ChunkDataMessage msg = new();

            if (length < HeaderSize)
            {
                return msg;
            }

            msg.ChunkX = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.ChunkY = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.ChunkZ = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;

            int payloadLen = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;

            if (payloadLen > 0 && offset + payloadLen <= buffer.Length)
            {
                msg.Payload = new byte[payloadLen];
                Buffer.BlockCopy(buffer, offset, msg.Payload, 0, payloadLen);
            }
            else
            {
                msg.Payload = Array.Empty<byte>();
            }

            return msg;
        }
    }
}
