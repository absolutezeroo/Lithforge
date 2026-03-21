using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Client-to-Server request to open a block entity container at a world position.
    ///     Wire format: [PositionX:4][PositionY:4][PositionZ:4] (12 bytes)
    /// </summary>
    public struct ContainerOpenCmdMessage : INetworkMessage
    {
        /// <summary>Fixed payload size: 3 ints = 12 bytes.</summary>
        public const int Size = 12;

        /// <summary>World X coordinate of the block entity.</summary>
        public int PositionX;

        /// <summary>World Y coordinate of the block entity.</summary>
        public int PositionY;

        /// <summary>World Z coordinate of the block entity.</summary>
        public int PositionZ;

        /// <summary>Returns the MessageType for this message.</summary>
        public MessageType Type
        {
            get { return MessageType.ContainerOpenCmd; }
        }

        /// <summary>Returns the total serialized payload size.</summary>
        public int GetSerializedSize()
        {
            return Size;
        }

        /// <summary>Writes the message payload into the buffer.</summary>
        public int Serialize(byte[] buffer, int offset)
        {
            MessageSerializer.WriteInt(buffer, offset, PositionX);
            MessageSerializer.WriteInt(buffer, offset + 4, PositionY);
            MessageSerializer.WriteInt(buffer, offset + 8, PositionZ);
            return Size;
        }

        /// <summary>Reads the message from the buffer.</summary>
        public static ContainerOpenCmdMessage Deserialize(byte[] buffer, int offset, int length)
        {
            ContainerOpenCmdMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.PositionX = MessageSerializer.ReadInt(buffer, offset);
            msg.PositionY = MessageSerializer.ReadInt(buffer, offset + 4);
            msg.PositionZ = MessageSerializer.ReadInt(buffer, offset + 8);
            return msg;
        }
    }
}
