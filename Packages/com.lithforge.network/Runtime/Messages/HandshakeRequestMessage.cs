using System;
using System.Text;

using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Client→Server handshake request. Sent on reliable pipeline immediately after
    ///     the transport Connect event. Contains protocol version, content hash, and player name.
    ///     Wire format: [ProtocolVersion:2][ContentHash.High:8][ContentHash.Low:8][NameLength:1][Name:N]
    /// </summary>
    public struct HandshakeRequestMessage : INetworkMessage
    {
        /// <summary>
        /// Minimum payload size without the player name string.
        /// </summary>
        public const int MinSize = 2 + 8 + 8 + 1; // 19 bytes without name

        /// <summary>
        /// The client's network protocol version.
        /// </summary>
        public ushort ProtocolVersion;

        /// <summary>
        /// Hash of the client's content definitions for compatibility verification.
        /// </summary>
        public ContentHash ContentHash;

        /// <summary>
        /// The player's display name (UTF-8 encoded, max MaxPlayerNameLength bytes).
        /// </summary>
        public string PlayerName;

        /// <summary>
        /// Returns the MessageType for this message.
        /// </summary>
        public MessageType Type
        {
            get { return MessageType.HandshakeRequest; }
        }

        /// <summary>
        /// Returns the payload size including the variable-length player name.
        /// </summary>
        public int GetSerializedSize()
        {
            int nameLength = 0;

            if (!string.IsNullOrEmpty(PlayerName))
            {
                nameLength = Encoding.UTF8.GetByteCount(PlayerName);

                if (nameLength > NetworkConstants.MaxPlayerNameLength)
                {
                    nameLength = NetworkConstants.MaxPlayerNameLength;
                }
            }

            return MinSize + nameLength;
        }

        /// <summary>
        /// Writes the message payload into the buffer at the given offset.
        /// </summary>
        public int Serialize(byte[] buffer, int offset)
        {
            int start = offset;
            MessageSerializer.WriteUShort(buffer, offset, ProtocolVersion);
            offset += 2;
            MessageSerializer.WriteULong(buffer, offset, ContentHash.High);
            offset += 8;
            MessageSerializer.WriteULong(buffer, offset, ContentHash.Low);
            offset += 8;

            if (string.IsNullOrEmpty(PlayerName))
            {
                buffer[offset] = 0;
                offset += 1;
            }
            else
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(PlayerName);
                int nameLength = Math.Min(nameBytes.Length, NetworkConstants.MaxPlayerNameLength);
                buffer[offset] = (byte)nameLength;
                offset += 1;
                Array.Copy(nameBytes, 0, buffer, offset, nameLength);
                offset += nameLength;
            }

            return offset - start;
        }

        /// <summary>
        /// Reads the message from the buffer. Returns a default message if the buffer is too small.
        /// </summary>
        public static HandshakeRequestMessage Deserialize(byte[] buffer, int offset, int length)
        {
            HandshakeRequestMessage msg = new();

            if (length < MinSize)
            {
                return msg;
            }

            msg.ProtocolVersion = MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;
            ulong high = MessageSerializer.ReadULong(buffer, offset);
            offset += 8;
            ulong low = MessageSerializer.ReadULong(buffer, offset);
            offset += 8;
            msg.ContentHash = new ContentHash(high, low);

            int nameLength = buffer[offset];
            offset += 1;

            if (nameLength > 0 && offset + nameLength <= buffer.Length)
            {
                msg.PlayerName = Encoding.UTF8.GetString(buffer, offset, nameLength);
            }
            else
            {
                msg.PlayerName = "";
            }

            return msg;
        }
    }
}
