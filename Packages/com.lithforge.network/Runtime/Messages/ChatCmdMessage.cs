using System;
using System.Text;

using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Client→Server chat command. Messages starting with "/" are admin commands;
    ///     all others are broadcast chat.
    ///     Wire format: [ContentLength:1][Content:N]
    /// </summary>
    public struct ChatCmdMessage : INetworkMessage
    {
        /// <summary>Minimum size: ContentLength(1).</summary>
        public const int MinSize = 1;

        /// <summary>The chat or command text (UTF-8, max MaxChatLength bytes).</summary>
        public string Content;

        /// <summary>Returns the MessageType for this message.</summary>
        public MessageType Type
        {
            get { return MessageType.ChatCmd; }
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

            if (string.IsNullOrEmpty(Content))
            {
                buffer[offset] = 0;
                offset += 1;
            }
            else
            {
                byte[] contentBytes = Encoding.UTF8.GetBytes(Content);
                int contentLen = Math.Min(contentBytes.Length, NetworkConstants.MaxChatLength);
                buffer[offset] = (byte)contentLen;
                offset += 1;
                Array.Copy(contentBytes, 0, buffer, offset, contentLen);
                offset += contentLen;
            }

            return offset - start;
        }

        /// <summary>Reads the message from the buffer.</summary>
        public static ChatCmdMessage Deserialize(byte[] buffer, int offset, int length)
        {
            ChatCmdMessage msg = new();

            if (length < MinSize)
            {
                return msg;
            }

            int contentLen = buffer[offset];
            offset += 1;

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
