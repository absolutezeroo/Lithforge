using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server-to-Client container force-close notification.
    ///     Wire format: [WindowId:1] (1 byte)
    /// </summary>
    public struct ContainerCloseMessage : INetworkMessage
    {
        /// <summary>Fixed payload size: 1 byte.</summary>
        public const int Size = 1;

        /// <summary>The window ID being force-closed.</summary>
        public byte WindowId;

        /// <summary>Returns the MessageType for this message.</summary>
        public MessageType Type
        {
            get { return MessageType.ContainerClose; }
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
        public static ContainerCloseMessage Deserialize(byte[] buffer, int offset, int length)
        {
            ContainerCloseMessage msg = new();

            if (length < Size)
            {
                return msg;
            }

            msg.WindowId = buffer[offset];
            return msg;
        }
    }
}
