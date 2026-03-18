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
        private readonly Queue<DirectPacket> _dataQueue = new();

        private readonly Queue<NetworkEventType> _eventQueue = new();

        public void Enqueue(byte[] data, int offset, int length)
        {
            byte[] copy = new byte[length];
            Buffer.BlockCopy(data, offset, copy, 0, length);
            _dataQueue.Enqueue(new DirectPacket(copy, 0, length));
        }

        public bool TryDequeue(out DirectPacket packet)
        {
            return _dataQueue.TryDequeue(out packet);
        }

        public void EnqueueEvent(NetworkEventType eventType)
        {
            _eventQueue.Enqueue(eventType);
        }

        public bool TryDequeueEvent(out NetworkEventType eventType)
        {
            return _eventQueue.TryDequeue(out eventType);
        }
    }

    internal readonly struct DirectPacket
    {
        public readonly byte[] Data;
        public readonly int Offset;
        public readonly int Length;

        public DirectPacket(byte[] data, int offset, int length)
        {
            Data = data;
            Offset = offset;
            Length = length;
        }
    }
}
