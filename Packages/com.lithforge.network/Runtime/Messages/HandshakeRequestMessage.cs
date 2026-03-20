using System;
using System.Text;

using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Client→Server handshake request. Sent on reliable pipeline immediately after
    ///     the transport Connect event. Contains protocol version, content hash, player name,
    ///     player UUID, and Ed25519 public key.
    ///     Wire format: [ProtocolVersion:2][ContentHash.High:8][ContentHash.Low:8][NameLength:1][Name:N]
    ///                  [UuidLength:1][Uuid:N][PubKeyLength:1][PubKey:32]
    /// </summary>
    public struct HandshakeRequestMessage : INetworkMessage
    {
        /// <summary>
        /// Minimum payload size without variable-length strings or public key.
        /// </summary>
        public const int MinSize = 2 + 8 + 8 + 1 + 1 + 1; // 21 bytes without name/uuid/pubkey

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

        /// <summary>The player's UUID string (max MaxUuidLength bytes).</summary>
        public string PlayerUuid;

        /// <summary>The player's Ed25519 public key (32 bytes). May be empty for local connections.</summary>
        public byte[] PublicKey;

        /// <summary>
        /// Returns the MessageType for this message.
        /// </summary>
        public MessageType Type
        {
            get { return MessageType.HandshakeRequest; }
        }

        /// <summary>
        /// Returns the payload size including variable-length player name, UUID, and public key.
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

            int uuidLength = 0;

            if (!string.IsNullOrEmpty(PlayerUuid))
            {
                uuidLength = Encoding.UTF8.GetByteCount(PlayerUuid);

                if (uuidLength > NetworkConstants.MaxUuidLength)
                {
                    uuidLength = NetworkConstants.MaxUuidLength;
                }
            }

            int pubKeyLength = PublicKey?.Length ?? 0;

            return MinSize + nameLength + uuidLength + pubKeyLength;
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

            // UUID
            if (string.IsNullOrEmpty(PlayerUuid))
            {
                buffer[offset] = 0;
                offset += 1;
            }
            else
            {
                byte[] uuidBytes = Encoding.UTF8.GetBytes(PlayerUuid);
                int uuidLength = Math.Min(uuidBytes.Length, NetworkConstants.MaxUuidLength);
                buffer[offset] = (byte)uuidLength;
                offset += 1;
                Array.Copy(uuidBytes, 0, buffer, offset, uuidLength);
                offset += uuidLength;
            }

            // Public key
            int pubKeyLen = PublicKey?.Length ?? 0;
            buffer[offset] = (byte)pubKeyLen;
            offset += 1;

            if (pubKeyLen > 0)
            {
                Array.Copy(PublicKey, 0, buffer, offset, pubKeyLen);
                offset += pubKeyLen;
            }

            return offset - start;
        }

        /// <summary>
        /// Reads the message from the buffer. Returns a default message if the buffer is too small.
        /// </summary>
        public static HandshakeRequestMessage Deserialize(byte[] buffer, int offset, int length)
        {
            HandshakeRequestMessage msg = new();
            int end = offset + length;

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

            if (nameLength > 0 && offset + nameLength <= end)
            {
                msg.PlayerName = Encoding.UTF8.GetString(buffer, offset, nameLength);
                offset += nameLength;
            }
            else
            {
                msg.PlayerName = "";
            }

            // UUID (optional — may be absent in older protocol versions)
            if (offset < end)
            {
                int uuidLength = buffer[offset];
                offset += 1;

                if (uuidLength > 0 && offset + uuidLength <= end)
                {
                    msg.PlayerUuid = Encoding.UTF8.GetString(buffer, offset, uuidLength);
                    offset += uuidLength;
                }
                else
                {
                    msg.PlayerUuid = "";
                }
            }
            else
            {
                msg.PlayerUuid = "";
            }

            // Public key (optional)
            if (offset < end)
            {
                int pubKeyLength = buffer[offset];
                offset += 1;

                if (pubKeyLength > 0 && offset + pubKeyLength <= end)
                {
                    msg.PublicKey = new byte[pubKeyLength];
                    Array.Copy(buffer, offset, msg.PublicKey, 0, pubKeyLength);
                }
            }

            return msg;
        }
    }
}
