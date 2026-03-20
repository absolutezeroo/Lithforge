using System;
using System.Text;

using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server→Client chat message broadcast.
    ///     Wire format: [SenderPlayerId:2][ContentLength:2][Content:N]
    /// </summary>
    public struct ChatMessage : INetworkMessage
    {
        /// <summary>Minimum size: SenderPlayerId(2) + ContentLength(2).</summary>
        public const int MinSize = 4;

        /// <summary>Player ID of the sender (0 = system message).</summary>
        public ushort SenderPlayerId;

        /// <summary>The chat message text content (UTF-8, max MaxChatLength bytes).</summary>
        public string Content;

        /// <summary>Returns the MessageType for this message.</summary>
        public MessageType Type
        {
            get { return MessageType.Chat; }
        }

        /// <summary>Returns the total serialized payload size.</summary>
        public int GetSerializedSize()
        {
            int contentLen = 0;

            if (!string.IsNullOrEmpty(Content))
            {
                contentLen = Encoding.UTF8.GetByteCount(Content);

                if (contentLen > NetworkConstants.MaxChatLength)
                {
                    contentLen = NetworkConstants.MaxChatLength;
                }
            }

            return MinSize + contentLen;
        }

        /// <summary>Writes the message payload into the buffer.</summary>
        public int Serialize(byte[] buffer, int offset)
        {
            int start = offset;
            MessageSerializer.WriteUShort(buffer, offset, SenderPlayerId);
            offset += 2;

            if (string.IsNullOrEmpty(Content))
            {
                MessageSerializer.WriteUShort(buffer, offset, 0);
                offset += 2;
            }
            else
            {
                byte[] contentBytes = Encoding.UTF8.GetBytes(Content);
                int contentLen = Math.Min(contentBytes.Length, NetworkConstants.MaxChatLength);
                MessageSerializer.WriteUShort(buffer, offset, (ushort)contentLen);
                offset += 2;
                Array.Copy(contentBytes, 0, buffer, offset, contentLen);
                offset += contentLen;
            }

            return offset - start;
        }

        /// <summary>Reads the message from the buffer.</summary>
        public static ChatMessage Deserialize(byte[] buffer, int offset, int length)
        {
            ChatMessage msg = new();

            if (length < MinSize)
            {
                return msg;
            }

            msg.SenderPlayerId = MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;
            int contentLen = MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;

            if (contentLen > 0 && offset + contentLen <= buffer.Length)
            {
                msg.Content = Encoding.UTF8.GetString(buffer, offset, contentLen);
            }
            else
            {
                msg.Content = "";
            }

            return msg;
        }
    }
}
