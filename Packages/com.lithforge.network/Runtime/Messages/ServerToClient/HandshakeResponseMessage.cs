using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server→Client handshake response. Sent on reliable pipeline after validating
    ///     the client's HandshakeRequest.
    ///     Wire format: [Accepted:1][RejectReason:1][PlayerId:2][ServerTick:4][WorldSeed:8]
    /// </summary>
    public struct HandshakeResponseMessage : INetworkMessage
    {
        /// <summary>
        /// Total payload size in bytes.
        /// </summary>
        public const int Size = 1 + 1 + 2 + 4 + 8; // 16 bytes

        /// <summary>
        /// Whether the handshake was accepted by the server.
        /// </summary>
        public bool Accepted;

        /// <summary>
        /// Reason code if the handshake was rejected; None if accepted.
        /// </summary>
        public HandshakeRejectReason RejectReason;

        /// <summary>
        /// Server-assigned player identifier (valid only when accepted).
        /// </summary>
        public ushort PlayerId;

        /// <summary>
        /// Current server tick at the time of acceptance.
        /// </summary>
        public uint ServerTick;

        /// <summary>
        /// World seed for deterministic world generation on the client.
        /// </summary>
        public ulong WorldSeed;

        /// <summary>
        /// Returns the MessageType for this message.
        /// </summary>
        public MessageType Type
        {
            get { return MessageType.HandshakeResponse; }
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
            buffer[offset] = Accepted ? (byte)1 : (byte)0;
            buffer[offset + 1] = (byte)RejectReason;
            MessageSerializer.WriteUShort(buffer, offset + 2, PlayerId);
            MessageSerializer.WriteUInt(buffer, offset + 4, ServerTick);
            MessageSerializer.WriteULong(buffer, offset + 8, WorldSeed);
            return Size;
        }

        /// <summary>
        /// Reads the message from the buffer. Returns a default message if the buffer is too small.
        /// </summary>
        public static HandshakeResponseMessage Deserialize(byte[] buffer, int offset, int length)
        {
            HandshakeResponseMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.Accepted = buffer[offset] != 0;
            msg.RejectReason = (HandshakeRejectReason)buffer[offset + 1];
            msg.PlayerId = MessageSerializer.ReadUShort(buffer, offset + 2);
            msg.ServerTick = MessageSerializer.ReadUInt(buffer, offset + 4);
            msg.WorldSeed = MessageSerializer.ReadULong(buffer, offset + 8);
            return msg;
        }
    }
}
