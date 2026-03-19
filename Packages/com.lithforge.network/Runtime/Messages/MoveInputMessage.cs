using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Client→Server movement input. Sent every tick on unreliable sequenced pipeline.
    ///     The server reconstructs InputSnapshot from the flags and look direction,
    ///     then runs player physics authoritatively.
    ///     Wire format: [SequenceId:2][Yaw:4][Pitch:4][Flags:1] = 11 bytes.
    ///     PlayerId is NOT on the wire — the server derives it from the ConnectionId.
    /// </summary>
    public struct MoveInputMessage : INetworkMessage
    {
        /// <summary>
        /// Total payload size in bytes.
        /// </summary>
        public const int Size = 2 + 4 + 4 + 1; // 11 bytes

        /// <summary>
        /// Client-assigned sequence number for prediction reconciliation.
        /// </summary>
        public ushort SequenceId;

        /// <summary>
        /// Camera yaw angle in degrees.
        /// </summary>
        public float Yaw;

        /// <summary>
        /// Camera pitch angle in degrees.
        /// </summary>
        public float Pitch;

        /// <summary>
        /// Bit-packed input flags (forward, back, left, right, jump, sprint, sneak).
        /// </summary>
        public byte Flags;

        /// <summary>
        /// Returns the MessageType for this message.
        /// </summary>
        public MessageType Type
        {
            get { return MessageType.MoveInput; }
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
            MessageSerializer.WriteFloat(buffer, offset, Yaw);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, Pitch);
            offset += 4;
            buffer[offset] = Flags;
            offset += 1;
            return offset - start;
        }

        /// <summary>
        /// Reads the message from the buffer. Returns a default message if the buffer is too small.
        /// </summary>
        public static MoveInputMessage Deserialize(byte[] buffer, int offset, int length)
        {
            MoveInputMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.SequenceId = MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;
            msg.Yaw = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.Pitch = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.Flags = buffer[offset];
            return msg;
        }
    }
}
