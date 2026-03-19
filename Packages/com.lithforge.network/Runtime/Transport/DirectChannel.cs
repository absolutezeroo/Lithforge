using System;
using System.Collections.Generic;

namespace Lithforge.Network.Transport
{
    /// <summary>
    ///     In-memory packet queue for DirectTransport communication.
    ///     One channel represents a one-way data flow (e.g. client→server or server→client).
    /// </summary>
    internal sealed class DirectChannel
    {
        /// <summary>
        /// Queue of data packets waiting to be read by the receiving side.
        /// </summary>
        private readonly Queue<DirectPacket> _dataQueue = new();

        /// <summary>
        /// Queue of synthetic events (Connect, Disconnect) for the receiving side.
        /// </summary>
        private readonly Queue<NetworkEventType> _eventQueue = new();

        /// <summary>
        /// Enqueues a copy of the data segment as a packet for the receiving side.
        /// </summary>
        public void Enqueue(byte[] data, int offset, int length)
        {
            byte[] copy = new byte[length];
            Buffer.BlockCopy(data, offset, copy, 0, length);
            _dataQueue.Enqueue(new DirectPacket(copy, 0, length));
        }

        /// <summary>
        /// Attempts to dequeue the next data packet. Returns false if the queue is empty.
        /// </summary>
        public bool TryDequeue(out DirectPacket packet)
        {
            return _dataQueue.TryDequeue(out packet);
        }

        /// <summary>
        /// Enqueues a synthetic network event (e.g., Disconnect) for the receiving side.
        /// </summary>
        public void EnqueueEvent(NetworkEventType eventType)
        {
            _eventQueue.Enqueue(eventType);
        }

        /// <summary>
        /// Attempts to dequeue the next synthetic event. Returns false if the queue is empty.
        /// </summary>
        public bool TryDequeueEvent(out NetworkEventType eventType)
        {
            return _eventQueue.TryDequeue(out eventType);
        }
    }

    /// <summary>
    /// A single in-memory packet containing a copied byte array segment.
    /// </summary>
    internal readonly struct DirectPacket
    {
        /// <summary>
        /// The packet data buffer.
        /// </summary>
        public readonly byte[] Data;

        /// <summary>
        /// Start offset within the data buffer.
        /// </summary>
        public readonly int Offset;

        /// <summary>
        /// Number of valid bytes starting from offset.
        /// </summary>
        public readonly int Length;

        /// <summary>
        /// Creates a new DirectPacket wrapping the given data segment.
        /// </summary>
        public DirectPacket(byte[] data, int offset, int length)
        {
            Data = data;
            Offset = offset;
            Length = length;
        }
    }
}
