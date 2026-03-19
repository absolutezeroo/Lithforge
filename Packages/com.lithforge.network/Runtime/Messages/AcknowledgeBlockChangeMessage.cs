using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server→Client block command acknowledgement. Sent on reliable sequenced pipeline.
    ///     Confirms or rejects a client's optimistic block prediction. If rejected,
    ///     <see cref="CorrectedState" /> contains the server's actual block state so the
    ///     client can revert its local prediction.
    ///     Wire format: [SequenceId:2][Accepted:1][PositionX:4][PositionY:4][PositionZ:4][CorrectedState:2] = 17 bytes.
    /// </summary>
    public struct AcknowledgeBlockChangeMessage : INetworkMessage
    {
        /// <summary>
        /// Total payload size in bytes.
        /// </summary>
        public const int Size = 2 + 1 + 4 + 4 + 4 + 2; // 17 bytes

        /// <summary>
        /// Client-assigned sequence number to correlate with the original command.
        /// </summary>
        public ushort SequenceId;

        /// <summary>
        /// Non-zero if the block command was accepted, zero if rejected.
        /// </summary>
        public byte Accepted;

        /// <summary>
        /// World-space X coordinate of the affected block.
        /// </summary>
        public int PositionX;

        /// <summary>
        /// World-space Y coordinate of the affected block.
        /// </summary>
        public int PositionY;

        /// <summary>
        /// World-space Z coordinate of the affected block.
        /// </summary>
        public int PositionZ;

        /// <summary>
        /// The authoritative block state after the server processed the command.
        /// </summary>
        public ushort CorrectedState;

        /// <summary>
        /// Returns the MessageType for this message.
        /// </summary>
        public MessageType Type
        {
            get { return MessageType.AcknowledgeBlockChange; }
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
            MessageSerializer.WriteUShort(buffer, offset, SequenceId);
            offset += 2;
            buffer[offset] = Accepted;
            offset += 1;
            MessageSerializer.WriteUInt(buffer, offset, (uint)PositionX);
            offset += 4;
            MessageSerializer.WriteUInt(buffer, offset, (uint)PositionY);
            offset += 4;
            MessageSerializer.WriteUInt(buffer, offset, (uint)PositionZ);
            offset += 4;
            MessageSerializer.WriteUShort(buffer, offset, CorrectedState);
            offset += 2;
            return offset - start;
        }

        /// <summary>
        /// Reads the message from the buffer. Returns a default message if the buffer is too small.
        /// </summary>
        public static AcknowledgeBlockChangeMessage Deserialize(byte[] buffer, int offset, int length)
        {
            AcknowledgeBlockChangeMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.SequenceId = MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;
            msg.Accepted = buffer[offset];
            offset += 1;
            msg.PositionX = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.PositionY = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.PositionZ = (int)MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.CorrectedState = MessageSerializer.ReadUShort(buffer, offset);

            return msg;
        }
    }
}
