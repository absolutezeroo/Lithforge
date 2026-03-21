using System;
using System.Text;

using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server→Client spawn notification for a remote player entering the observer's
    ///     interest range. Sent on reliable sequenced pipeline. Contains initial state
    ///     so the client can create the entity with a valid first snapshot.
    ///     Wire format: [PlayerId:2][NameLength:1][NameBytes:N][PosX:4][PosY:4][PosZ:4]
    ///     [Yaw:4][Pitch:4][Flags:1] = 24 + N bytes (N ≤ 64).
    /// </summary>
    public struct SpawnPlayerMessage : INetworkMessage
    {
        /// <summary>
        /// Maximum number of UTF-8 bytes allowed for the player name.
        /// </summary>
        public const int MaxNameBytes = 64;

        /// <summary>
        /// Size of the fixed-length portion of the payload (excluding the variable-length name).
        /// </summary>
        public const int FixedSize = 2 + 1 + 12 + 4 + 4 + 1; // 24 bytes without name

        /// <summary>
        /// Network-assigned player identifier.
        /// </summary>
        public ushort PlayerId;

        /// <summary>
        /// Display name of the spawning player (UTF-8, max MaxNameBytes).
        /// </summary>
        public string PlayerName;

        /// <summary>
        /// Initial X position of the spawning player.
        /// </summary>
        public float PositionX;

        /// <summary>
        /// Initial Y position of the spawning player.
        /// </summary>
        public float PositionY;

        /// <summary>
        /// Initial Z position of the spawning player.
        /// </summary>
        public float PositionZ;

        /// <summary>
        /// Initial yaw angle in degrees.
        /// </summary>
        public float Yaw;

        /// <summary>
        /// Initial pitch angle in degrees.
        /// </summary>
        public float Pitch;

        /// <summary>
        /// Initial physics state flags (on ground, etc.).
        /// </summary>
        public byte Flags;

        /// <summary>
        /// Returns the MessageType for this message.
        /// </summary>
        public MessageType Type
        {
            get { return MessageType.SpawnPlayer; }
        }

        /// <summary>
        /// Returns the payload size including the variable-length player name.
        /// </summary>
        public int GetSerializedSize()
        {
            int nameLen = 0;

            if (!string.IsNullOrEmpty(PlayerName))
            {
                nameLen = Math.Min(Encoding.UTF8.GetByteCount(PlayerName), MaxNameBytes);
            }

            return FixedSize + nameLen;
        }

        /// <summary>
        /// Writes the message payload into the buffer at the given offset.
        /// </summary>
        public int Serialize(byte[] buffer, int offset)
        {
            int start = offset;
            MessageSerializer.WriteUShort(buffer, offset, PlayerId);
            offset += 2;

            // Name: length-prefixed UTF-8 (encode to temp array to safely truncate
            // multi-byte characters that would exceed MaxNameBytes)
            int nameLen = 0;

            if (!string.IsNullOrEmpty(PlayerName))
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(PlayerName);
                nameLen = Math.Min(nameBytes.Length, MaxNameBytes);
                buffer[offset] = (byte)nameLen;
                Buffer.BlockCopy(nameBytes, 0, buffer, offset + 1, nameLen);
            }
            else
            {
                buffer[offset] = 0;
            }

            offset += 1 + nameLen;

            MessageSerializer.WriteFloat(buffer, offset, PositionX);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, PositionY);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, PositionZ);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, Yaw);
            offset += 4;
            MessageSerializer.WriteFloat(buffer, offset, Pitch);
            offset += 4;
            buffer[offset] = Flags;
            offset += 1;
            return offset - start;
        }

        /// <summary>
        /// Reads the message from the buffer. Returns a default message if the buffer is too small.
        /// </summary>
        public static SpawnPlayerMessage Deserialize(byte[] buffer, int offset, int length)
        {
            SpawnPlayerMessage msg = new();

            if (length < FixedSize)
            {
                return msg;
            }

            msg.PlayerId = MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;

            int nameLen = buffer[offset];
            offset += 1;

            if (nameLen > MaxNameBytes)
            {
                nameLen = MaxNameBytes;
            }

            if (nameLen > 0 && offset + nameLen <= buffer.Length)
            {
                msg.PlayerName = Encoding.UTF8.GetString(buffer, offset, nameLen);
            }
            else
            {
                msg.PlayerName = "";
            }

            offset += nameLen;

            msg.PositionX = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.PositionY = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.PositionZ = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.Yaw = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.Pitch = MessageSerializer.ReadFloat(buffer, offset);
            offset += 4;
            msg.Flags = buffer[offset];
            return msg;
        }
    }
}
