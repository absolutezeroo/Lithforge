using System;
using System.Text;

using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Client-to-Server crafting output take command with recipe identification.
    ///     Wire format: [WindowId:1][IsShiftClick:1][RecipeNsLen:1][RecipeNs:N][RecipeNameLen:1][RecipeName:N]
    /// </summary>
    public struct CraftActionCmdMessage : INetworkMessage
    {
        /// <summary>Minimum payload size: WindowId(1) + IsShiftClick(1) + RecipeNsLen(1) + RecipeNameLen(1).</summary>
        public const int MinSize = 4;

        /// <summary>Window identifier for the crafting container.</summary>
        public byte WindowId;

        /// <summary>Whether this is a shift-click (craft all) action.</summary>
        public byte IsShiftClick;

        /// <summary>Recipe namespace (e.g. "lithforge").</summary>
        public string RecipeNs;

        /// <summary>Recipe name (e.g. "oak_planks").</summary>
        public string RecipeName;

        /// <summary>Returns the MessageType for this message.</summary>
        public MessageType Type
        {
            get { return MessageType.CraftActionCmd; }
        }

        /// <summary>Returns the total serialized payload size.</summary>
        public int GetSerializedSize()
        {
            int nsLen = string.IsNullOrEmpty(RecipeNs) ? 0 : Encoding.UTF8.GetByteCount(RecipeNs);
            int nameLen = string.IsNullOrEmpty(RecipeName) ? 0 : Encoding.UTF8.GetByteCount(RecipeName);
            return MinSize + nsLen + nameLen;
        }

        /// <summary>Writes the message payload into the buffer.</summary>
        public int Serialize(byte[] buffer, int offset)
        {
            int start = offset;

            buffer[offset] = WindowId;
            offset += 1;
            buffer[offset] = IsShiftClick;
            offset += 1;

            offset += WriteString(buffer, offset, RecipeNs);
            offset += WriteString(buffer, offset, RecipeName);

            return offset - start;
        }

        /// <summary>Reads the message from the buffer.</summary>
        public static CraftActionCmdMessage Deserialize(byte[] buffer, int offset, int length)
        {
            CraftActionCmdMessage msg = new();
            int end = offset + length;

            if (length < MinSize)
            {
                return msg;
            }

            msg.WindowId = buffer[offset];
            offset += 1;
            msg.IsShiftClick = buffer[offset];
            offset += 1;

            msg.RecipeNs = ReadString(buffer, ref offset, end);
            msg.RecipeName = ReadString(buffer, ref offset, end);

            return msg;
        }

        /// <summary>Writes a length-prefixed UTF-8 string. Returns bytes written.</summary>
        private static int WriteString(byte[] buffer, int offset, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                buffer[offset] = 0;
                return 1;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            buffer[offset] = (byte)bytes.Length;
            Array.Copy(bytes, 0, buffer, offset + 1, bytes.Length);
            return 1 + bytes.Length;
        }

        /// <summary>Reads a length-prefixed UTF-8 string from the buffer.</summary>
        private static string ReadString(byte[] buffer, ref int offset, int end)
        {
            if (offset >= end)
            {
                return "";
            }

            int len = buffer[offset];
            offset += 1;

            if (len > 0 && offset + len <= end)
            {
                string result = Encoding.UTF8.GetString(buffer, offset, len);
                offset += len;
                return result;
            }

            return "";
        }
    }
}
