using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server-to-Client furnace burn/smelt progress update.
    ///     Wire format: [WindowId:1][BurnProgress:2][SmeltProgress:2] (5 bytes)
    ///     Progress values are ushort: 0 = 0%, 65535 = 100%.
    /// </summary>
    public struct ContainerProgressMessage : INetworkMessage
    {
        /// <summary>Fixed payload size: 5 bytes.</summary>
        public const int Size = 5;

        /// <summary>The window ID of the furnace container.</summary>
        public byte WindowId;

        /// <summary>Burn progress (0–65535 mapped to 0%–100%).</summary>
        public ushort BurnProgress;

        /// <summary>Smelt progress (0–65535 mapped to 0%–100%).</summary>
        public ushort SmeltProgress;

        /// <summary>Returns the MessageType for this message.</summary>
        public MessageType Type
        {
            get { return MessageType.ContainerProgress; }
        }

        /// <summary>Returns the total serialized payload size.</summary>
        public int GetSerializedSize()
        {
            return Size;
        }

        /// <summary>Writes the message payload into the buffer.</summary>
        public int Serialize(byte[] buffer, int offset)
        {
            buffer[offset] = WindowId;
            MessageSerializer.WriteUShort(buffer, offset + 1, BurnProgress);
            MessageSerializer.WriteUShort(buffer, offset + 3, SmeltProgress);
            return Size;
        }

        /// <summary>Reads the message from the buffer.</summary>
        public static ContainerProgressMessage Deserialize(byte[] buffer, int offset, int length)
        {
            ContainerProgressMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.WindowId = buffer[offset];
            msg.BurnProgress = MessageSerializer.ReadUShort(buffer, offset + 1);
            msg.SmeltProgress = MessageSerializer.ReadUShort(buffer, offset + 3);
            return msg;
        }
    }
}
