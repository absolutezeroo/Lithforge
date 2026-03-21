using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Client-to-Server notification that a container window was closed.
    ///     Wire format: [WindowId:1] (1 byte)
    /// </summary>
    public struct ContainerCloseCmdMessage : INetworkMessage
    {
        /// <summary>Fixed payload size: 1 byte.</summary>
        public const int Size = 1;

        /// <summary>The window ID being closed.</summary>
        public byte WindowId;

        /// <summary>Returns the MessageType for this message.</summary>
        public MessageType Type
        {
            get { return MessageType.ContainerCloseCmd; }
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
            return Size;
        }

        /// <summary>Reads the message from the buffer.</summary>
        public static ContainerCloseCmdMessage Deserialize(byte[] buffer, int offset, int length)
        {
            ContainerCloseCmdMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.WindowId = buffer[offset];
            return msg;
        }
    }
}
