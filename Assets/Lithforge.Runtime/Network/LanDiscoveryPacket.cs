using System;
using System.Text;

namespace Lithforge.Runtime.Network
{
    /// <summary>
    /// Wire format for LAN server discovery broadcasts. Contains a 4-byte magic
    /// number, 1-byte protocol version, and a UTF-8 JSON payload. The total
    /// packet stays under 512 bytes to avoid UDP fragmentation on all networks.
    /// </summary>
    public static class LanDiscoveryPacket
    {
        /// <summary>Magic bytes: "LFLF" (0x4C464C46).</summary>
        public static readonly byte[] Magic = { 0x4C, 0x46, 0x4C, 0x46 };

        /// <summary>Current discovery protocol version.</summary>
        public const byte ProtocolVersion = 1;

        /// <summary>Header size: 4 magic + 1 version = 5 bytes.</summary>
        public const int HeaderSize = 5;

        /// <summary>Maximum total packet size. Kept under 512 to avoid fragmentation.</summary>
        public const int MaxPacketSize = 512;

        /// <summary>Maximum JSON payload size (total minus header).</summary>
        public const int MaxPayloadSize = MaxPacketSize - HeaderSize;

        /// <summary>
        /// Serializes a <see cref="LanServerInfo"/> into a UDP broadcast packet.
        /// Returns the number of bytes written, or 0 if the payload exceeds the
        /// maximum packet size.
        /// </summary>
        public static int Serialize(LanServerInfo info, byte[] buffer)
        {
            string json = UnityEngine.JsonUtility.ToJson(info);
            int jsonBytes = Encoding.UTF8.GetByteCount(json);

            if (jsonBytes > MaxPayloadSize)
            {
                return 0;
            }

            buffer[0] = Magic[0];
            buffer[1] = Magic[1];
            buffer[2] = Magic[2];
            buffer[3] = Magic[3];
            buffer[4] = ProtocolVersion;

            int written = Encoding.UTF8.GetBytes(json, 0, json.Length, buffer, HeaderSize);
            return HeaderSize + written;
        }

        /// <summary>
        /// Attempts to deserialize a UDP packet into a <see cref="LanServerInfo"/>.
        /// Returns true on success; false if the magic number or version doesn't match.
        /// </summary>
        public static bool TryDeserialize(byte[] data, int length, out LanServerInfo info)
        {
            info = default;

            if (length < HeaderSize)
            {
                return false;
            }

            if (data[0] != Magic[0] || data[1] != Magic[1] ||
                data[2] != Magic[2] || data[3] != Magic[3])
            {
                return false;
            }

            if (data[4] != ProtocolVersion)
            {
                return false;
            }

            int payloadLength = length - HeaderSize;

            if (payloadLength <= 0)
            {
                return false;
            }

            try
            {
                string json = Encoding.UTF8.GetString(data, HeaderSize, payloadLength);
                info = UnityEngine.JsonUtility.FromJson<LanServerInfo>(json);
                return info != null;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
