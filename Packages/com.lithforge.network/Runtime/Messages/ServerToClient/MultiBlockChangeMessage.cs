using System;

using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server→Client batched block change notification. Sent on reliable sequenced pipeline.
    ///     The payload is the raw output of
    ///     <see cref="Lithforge.Voxel.Network.ChunkNetSerializer.SerializeBlockChangeBatch" />.
    ///     Used when 2+ blocks changed in a chunk section this tick.
    ///     Wire format: raw batch bytes (variable length).
    /// </summary>
    public struct MultiBlockChangeMessage : INetworkMessage
    {
        /// <summary>
        ///     Raw serialized batch data from ChunkNetSerializer.SerializeBlockChangeBatch.
        /// </summary>
        public byte[] BatchData;

        /// <summary>
        /// Returns the MessageType for this message.
        /// </summary>
        public MessageType Type
        {
            get { return MessageType.MultiBlockChange; }
        }

        /// <summary>
        /// Returns the payload size (the length of the raw batch data).
        /// </summary>
        public int GetSerializedSize()
        {
            return BatchData?.Length ?? 0;
        }

        /// <summary>
        /// Writes the raw batch data into the buffer at the given offset.
        /// </summary>
        public int Serialize(byte[] buffer, int offset)
        {
            if (BatchData == null || BatchData.Length == 0)
            {
                return 0;
            }

            Buffer.BlockCopy(BatchData, 0, buffer, offset, BatchData.Length);
            return BatchData.Length;
        }

        /// <summary>
        /// Reads the message by copying the raw batch data from the buffer.
        /// </summary>
        public static MultiBlockChangeMessage Deserialize(byte[] buffer, int offset, int length)
        {
            MultiBlockChangeMessage msg = new();

            if (length <= 0)
            {
                msg.BatchData = Array.Empty<byte>();
                return msg;
            }

            msg.BatchData = new byte[length];
            Buffer.BlockCopy(buffer, offset, msg.BatchData, 0, length);
            return msg;
        }
    }
}
