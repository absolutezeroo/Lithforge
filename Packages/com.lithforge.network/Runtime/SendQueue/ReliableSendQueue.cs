using System;
using System.Collections.Generic;

using Lithforge.Core.Logging;
using Lithforge.Network.Transport;

namespace Lithforge.Network.SendQueue
{
    /// <summary>
    ///     Buffers sends that failed with error -5 (send queue full) for retry next frame.
    ///     Each entry is retried up to MaxSendRetries times before being dropped.
    ///     Called once per frame in server/client Update after transport.Update().
    /// </summary>
    public sealed class ReliableSendQueue
    {
        private readonly ILogger _logger;
        private readonly List<PendingSend> _pending = new();

        public ReliableSendQueue(ILogger logger)
        {
            _logger = logger;
        }

        public int Count
        {
            get { return _pending.Count; }
        }

        /// <summary>
        ///     Enqueues a failed send for retry. The data is copied into a new buffer
        ///     so the caller's buffer can be reused immediately.
        /// </summary>
        public void Enqueue(ConnectionId connectionId, int pipelineId, byte[] data, int offset, int length)
        {
            byte[] copy = new byte[length];
            Array.Copy(data, offset, copy, 0, length);

            _pending.Add(new PendingSend
            {
                ConnectionId = connectionId,
                PipelineId = pipelineId,
                Data = copy,
                Offset = 0,
                Length = length,
                RetryCount = 0,
            });
        }

        /// <summary>
        ///     Retries all buffered sends. Removes entries that succeed or exceed MaxSendRetries.
        ///     Returns the number of entries that were permanently dropped.
        /// </summary>
        public int Flush(INetworkTransport transport)
        {
            int dropped = 0;
            int i = 0;

            while (i < _pending.Count)
            {
                PendingSend entry = _pending[i];
                bool success = transport.Send(
                    entry.ConnectionId, entry.PipelineId, entry.Data, entry.Offset, entry.Length);

                if (success)
                {
                    _pending.RemoveAt(i);
                    continue;
                }

                entry.RetryCount++;

                if (entry.RetryCount >= NetworkConstants.MaxSendRetries)
                {
                    _logger.LogWarning(
                        $"Dropping message after {NetworkConstants.MaxSendRetries} retries " +
                        $"to connection {entry.ConnectionId}");
                    _pending.RemoveAt(i);
                    dropped++;
                    continue;
                }

                _pending[i] = entry;
                i++;
            }

            return dropped;
        }

        /// <summary>
        ///     Removes all buffered sends for the given connection.
        /// </summary>
        public void RemoveForConnection(ConnectionId connectionId)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].ConnectionId == connectionId)
                {
                    _pending.RemoveAt(i);
                }
            }
        }

        /// <summary>
        ///     Clears all buffered sends.
        /// </summary>
        public void Clear()
        {
            _pending.Clear();
        }

        private struct PendingSend
        {
            public ConnectionId ConnectionId;
            public int PipelineId;
            public byte[] Data;
            public int Offset;
            public int Length;
            public int RetryCount;
        }
    }
}
