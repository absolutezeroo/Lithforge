using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    /// Server→Client game ready signal. Sent on reliable sequenced pipeline.
    /// Tells the client that enough initial chunks have been streamed and the player
    /// should transition from Loading to Playing state. Contains the authoritative
    /// spawn position and current time of day.
    /// Wire format: [SpawnX:4][SpawnY:4][SpawnZ:4][TimeOfDay:4][ServerTick:4] = 20 bytes.
    /// </summary>
    public struct GameReadyMessage : INetworkMessage
    {
        public const int Size = 4 + 4 + 4 + 4 + 4; // 20 bytes

        public float SpawnX;
        public float SpawnY;
        public float SpawnZ;
        public float TimeOfDay;
        public uint ServerTick;

        public MessageType Type
        {
            get { return MessageType.GameReady; }
        }

        public int GetSerializedSize()
        {
            return Size;
        }

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

        public static GameReadyMessage Deserialize(byte[] buffer, int offset, int length)
        {
            GameReadyMessage msg = new GameReadyMessage();

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
