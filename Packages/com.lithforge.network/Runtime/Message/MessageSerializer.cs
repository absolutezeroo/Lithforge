using System;

namespace Lithforge.Network.Message
{
    /// <summary>
    /// Static helpers for serializing complete messages (header + payload) into send buffers.
    /// Maintains a reusable buffer pool to avoid per-send allocation.
    /// </summary>
    public static class MessageSerializer
    {
        // Shared send buffer — only used on main thread, so no thread safety needed.
        // Resized as needed, never shrunk.
        private static byte[] _sendBuffer = new byte[1024];

        /// <summary>
        /// Serializes a complete message (header + payload) into the shared send buffer.
        /// Returns the total number of bytes written. The caller must consume the data
        /// before the next call to WriteMessage.
        /// </summary>
        public static int WriteMessage(INetworkMessage message, out byte[] buffer)
        {
            int payloadSize = message.GetSerializedSize();
            int totalSize = MessageHeader.Size + payloadSize;

            EnsureBufferCapacity(totalSize);

            MessageHeader.Write(_sendBuffer, 0, message.Type, (ushort)payloadSize, 0);
            message.Serialize(_sendBuffer, MessageHeader.Size);

            buffer = _sendBuffer;
            return totalSize;
        }

        /// <summary>
        /// Writes a little-endian ushort to the buffer at the given offset.
        /// </summary>
        public static void WriteUShort(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        /// <summary>
        /// Reads a little-endian ushort from the buffer at the given offset.
        /// </summary>
        public static ushort ReadUShort(byte[] buffer, int offset)
        {
            return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
        }

        /// <summary>
        /// Writes a little-endian uint to the buffer at the given offset.
        /// </summary>
        public static void WriteUInt(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        /// <summary>
        /// Reads a little-endian uint from the buffer at the given offset.
        /// </summary>
        public static uint ReadUInt(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset]
                | (buffer[offset + 1] << 8)
                | (buffer[offset + 2] << 16)
                | (buffer[offset + 3] << 24));
        }

        /// <summary>
        /// Writes a little-endian ulong to the buffer at the given offset.
        /// </summary>
        public static void WriteULong(byte[] buffer, int offset, ulong value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
            buffer[offset + 4] = (byte)((value >> 32) & 0xFF);
            buffer[offset + 5] = (byte)((value >> 40) & 0xFF);
            buffer[offset + 6] = (byte)((value >> 48) & 0xFF);
            buffer[offset + 7] = (byte)((value >> 56) & 0xFF);
        }

        /// <summary>
        /// Reads a little-endian ulong from the buffer at the given offset.
        /// </summary>
        public static ulong ReadULong(byte[] buffer, int offset)
        {
            return (ulong)buffer[offset]
                | ((ulong)buffer[offset + 1] << 8)
                | ((ulong)buffer[offset + 2] << 16)
                | ((ulong)buffer[offset + 3] << 24)
                | ((ulong)buffer[offset + 4] << 32)
                | ((ulong)buffer[offset + 5] << 40)
                | ((ulong)buffer[offset + 6] << 48)
                | ((ulong)buffer[offset + 7] << 56);
        }

        /// <summary>
        /// Writes a float as 4 little-endian bytes to the buffer.
        /// </summary>
        public static unsafe void WriteFloat(byte[] buffer, int offset, float value)
        {
            uint bits = *(uint*)&value;
            WriteUInt(buffer, offset, bits);
        }

        /// <summary>
        /// Reads a float from 4 little-endian bytes in the buffer.
        /// </summary>
        public static unsafe float ReadFloat(byte[] buffer, int offset)
        {
            uint bits = ReadUInt(buffer, offset);
            return *(float*)&bits;
        }

        private static void EnsureBufferCapacity(int required)
        {
            if (_sendBuffer.Length >= required)
            {
                return;
            }

            int newSize = _sendBuffer.Length;

            while (newSize < required)
            {
                newSize *= 2;
            }

            _sendBuffer = new byte[newSize];
        }
    }
}
