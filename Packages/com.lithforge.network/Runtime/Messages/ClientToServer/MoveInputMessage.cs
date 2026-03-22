using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Client→Server movement input with client-computed position. Sent every tick on
    ///     unreliable sequenced pipeline. The client runs physics locally and reports its
    ///     resulting position; the server validates it against speed limits and collision
    ///     geometry rather than re-simulating from inputs.
    ///     Wire format: [SequenceId:2][Yaw:4][Pitch:4][Flags:1][PosX:4][PosY:4][PosZ:4] = 23 bytes.
    ///     PlayerId is NOT on the wire — the server derives it from the ConnectionId.
    /// </summary>
    public struct MoveInputMessage : INetworkMessage
    {
        /// <summary>Total payload size in bytes.</summary>
        public const int Size = 2 + 4 + 4 + 1 + 4 + 4 + 4; // 23 bytes

        /// <summary>Client-assigned sequence number, echoed back in PlayerStateMessage.</summary>
        public ushort SequenceId;

        /// <summary>Camera yaw angle in degrees.</summary>
        public float Yaw;

        /// <summary>Camera pitch angle in degrees.</summary>
        public float Pitch;

        /// <summary>Bit-packed input flags (forward, back, left, right, jump, sprint, sneak).</summary>
        public byte Flags;

        /// <summary>Client-computed X position after this tick's physics step.</summary>
        public float PositionX;

        /// <summary>Client-computed Y position after this tick's physics step.</summary>
        public float PositionY;

        /// <summary>Client-computed Z position after this tick's physics step.</summary>
        public float PositionZ;

        /// <summary>Returns the MessageType for this message.</summary>
        public MessageType Type
        {
            get { return MessageType.MoveInput; }
        }

        /// <summary>Returns the fixed payload size in bytes.</summary>
        public int GetSerializedSize()
        {
            return Size;
        }

        /// <summary>Writes the message payload into the buffer at the given offset.</summary>
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
            MessageSerializer.WriteFloat(buffer, offset, PositionX);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, PositionY);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, PositionZ);
            offset += 4;
            return offset - start;
        }

        /// <summary>Reads the message from the buffer. Returns a default message if the buffer is too small.</summary>
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
            offset += 1;
            msg.PositionX = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.PositionY = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.PositionZ = MessageSerializer.ReadFloat(buffer, offset);
            return msg;
        }
    }
}
