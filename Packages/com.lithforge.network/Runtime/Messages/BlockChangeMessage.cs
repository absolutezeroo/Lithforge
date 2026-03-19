using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server→Client single block change notification. Sent on reliable sequenced pipeline.
    ///     Used when exactly one block changed in a chunk this tick (for 2+ changes,
    ///     use <see cref="MultiBlockChangeMessage" />).
    ///     Wire format: [PositionX:4][PositionY:4][PositionZ:4][StateId:2] = 14 bytes.
    /// </summary>
    public struct BlockChangeMessage : INetworkMessage
    {
        /// <summary>
        /// Total payload size in bytes.
        /// </summary>
        public const int Size = 4 + 4 + 4 + 2; // 14 bytes

        /// <summary>
        /// World-space X coordinate of the changed block.
        /// </summary>
        public int PositionX;

        /// <summary>
        /// World-space Y coordinate of the changed block.
        /// </summary>
        public int PositionY;

        /// <summary>
        /// World-space Z coordinate of the changed block.
        /// </summary>
        public int PositionZ;

        /// <summary>
        /// The new block state at this position.
        /// </summary>
        public ushort NewState;

        /// <summary>
        /// Returns the MessageType for this message.
        /// </summary>
        public MessageType Type
        {
            get { return MessageType.BlockChange; }
        }

        /// <summary>
        /// Returns the fixed payload size in bytes.
        /// </summary>
        public int GetSerializedSize()
        {
            return Size;
        }

        /// <summary>
        /// Writes the message payload into the buffer at the given offset.
        /// </summary>
        public int Serialize(byte[] buffer, int offset)
        {
            int start = offset;
            MessageSerializer.WriteUInt(buffer, offset, (uint)PositionX);
            offset += 4;
            MessageSerializer.WriteUInt(buffer, offset, (uint)PositionY);
            offset += 4;
            MessageSerializer.WriteUInt(buffer, offset, (uint)PositionZ);
            offset += 4;
            MessageSerializer.WriteUShort(buffer, offset, NewState);
            offset += 2;
            return offset - start;
        }

        /// <summary>
        /// Reads the message from the buffer. Returns a default message if the buffer is too small.
        /// </summary>
        public static BlockChangeMessage Deserialize(byte[] buffer, int offset, int length)
        {
            BlockChangeMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.PositionX = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.PositionY = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.PositionZ = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.NewState = MessageSerializer.ReadUShort(buffer, offset);
            return msg;
        }
    }
}
