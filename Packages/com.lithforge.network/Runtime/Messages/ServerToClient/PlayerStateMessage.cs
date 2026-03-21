using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server→Client authoritative player state. Sent every tick on unreliable sequenced pipeline.
    ///     Contains the server's authoritative position/velocity plus the last acknowledged
    ///     MoveInput sequence ID, enabling client-side prediction reconciliation.
    ///     Wire format: [PlayerId:2][ServerTick:4][LastProcessedSeqId:2][Position:12]
    ///     [Velocity:12][Yaw:4][Pitch:4][Flags:1] = 41 bytes.
    /// </summary>
    public struct PlayerStateMessage : INetworkMessage
    {
        /// <summary>
        /// Total payload size in bytes.
        /// </summary>
        public const int Size = 2 + 4 + 2 + 12 + 12 + 4 + 4 + 1; // 41 bytes

        /// <summary>
        /// Network-assigned player identifier.
        /// </summary>
        public ushort PlayerId;

        /// <summary>
        /// Server tick when this state was computed.
        /// </summary>
        public uint ServerTick;

        /// <summary>
        /// Sequence ID of the last MoveInput the server processed for this player.
        /// </summary>
        public ushort LastProcessedSeqId;

        /// <summary>
        /// Authoritative player X position.
        /// </summary>
        public float PositionX;

        /// <summary>
        /// Authoritative player Y position.
        /// </summary>
        public float PositionY;

        /// <summary>
        /// Authoritative player Z position.
        /// </summary>
        public float PositionZ;

        /// <summary>
        /// Authoritative player X velocity.
        /// </summary>
        public float VelocityX;

        /// <summary>
        /// Authoritative player Y velocity.
        /// </summary>
        public float VelocityY;

        /// <summary>
        /// Authoritative player Z velocity.
        /// </summary>
        public float VelocityZ;

        /// <summary>
        /// Camera yaw angle in degrees.
        /// </summary>
        public float Yaw;

        /// <summary>
        /// Camera pitch angle in degrees.
        /// </summary>
        public float Pitch;

        /// <summary>
        /// Physics state flags (on ground, swimming, etc.).
        /// </summary>
        public byte Flags;

        /// <summary>
        /// Returns the MessageType for this message.
        /// </summary>
        public MessageType Type
        {
            get { return MessageType.PlayerState; }
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
            MessageSerializer.WriteUShort(buffer, offset, PlayerId);
            offset += 2;
            MessageSerializer.WriteUInt(buffer, offset, ServerTick);
            offset += 4;
            MessageSerializer.WriteUShort(buffer, offset, LastProcessedSeqId);
            offset += 2;
            MessageSerializer.WriteFloat(buffer, offset, PositionX);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, PositionY);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, PositionZ);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, VelocityX);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, VelocityY);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, VelocityZ);
            offset += 4;
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
        public static PlayerStateMessage Deserialize(byte[] buffer, int offset, int length)
        {
            PlayerStateMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.PlayerId = MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;
            msg.ServerTick = MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.LastProcessedSeqId = MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;
            msg.PositionX = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.PositionY = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.PositionZ = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.VelocityX = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.VelocityY = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.VelocityZ = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.Yaw = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.Pitch = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.Flags = buffer[offset];
            return msg;
        }
    }
}
