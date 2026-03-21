using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server→Client game ready signal. Sent on reliable sequenced pipeline.
    ///     Tells the client that enough initial chunks have been streamed and the player
    ///     should transition from Loading to Playing state. Contains the authoritative
    ///     spawn position and current time of day.
    ///     Wire format: [SpawnX:4][SpawnY:4][SpawnZ:4][TimeOfDay:4][ServerTick:4] = 20 bytes.
    /// </summary>
    public struct GameReadyMessage : INetworkMessage
    {
        /// <summary>
        /// Total payload size in bytes.
        /// </summary>
        public const int Size = 4 + 4 + 4 + 4 + 4; // 20 bytes

        /// <summary>
        /// Spawn position X coordinate.
        /// </summary>
        public float SpawnX;

        /// <summary>
        /// Spawn position Y coordinate.
        /// </summary>
        public float SpawnY;

        /// <summary>
        /// Spawn position Z coordinate.
        /// </summary>
        public float SpawnZ;

        /// <summary>
        /// Current time of day in the 0-1 range.
        /// </summary>
        public float TimeOfDay;

        /// <summary>
        /// Current server tick at the time this message was sent.
        /// </summary>
        public uint ServerTick;

        /// <summary>
        /// Returns the MessageType for this message.
        /// </summary>
        public MessageType Type
        {
            get { return MessageType.GameReady; }
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
            MessageSerializer.WriteFloat(buffer, offset, SpawnX);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, SpawnY);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, SpawnZ);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, TimeOfDay);
            offset += 4;
            MessageSerializer.WriteUInt(buffer, offset, ServerTick);
            offset += 4;
            return offset - start;
        }

        /// <summary>
        /// Reads the message from the buffer. Returns a default message if the buffer is too small.
        /// </summary>
        public static GameReadyMessage Deserialize(byte[] buffer, int offset, int length)
        {
            GameReadyMessage msg = new();

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
            msg.TimeOfDay = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.ServerTick = MessageSerializer.ReadUInt(buffer, offset);
            return msg;
        }
    }
}
