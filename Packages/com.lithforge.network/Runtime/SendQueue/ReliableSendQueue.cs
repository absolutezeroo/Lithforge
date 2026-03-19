using System.Collections.Generic;

using Lithforge.Core.Logging;
using Lithforge.Network.Transport;

namespace Lithforge.Network.SendQueue
{
    /// <summary>
    ///     Buffers sends that failed with error -5 (send queue full) for retry with
    ///     exponential backoff. Each entry is retried up to <see cref="NetworkConstants.MaxSendRetries" />
    ///     times with increasing delay (0.1s, 0.2s, 0.4s) before being dropped.
    ///     Called once per frame in server/client Update after transport.Update().
    /// </summary>
    public sealed class ReliableSendQueue
    {
        /// <summary>
        /// Logger instance for warning about dropped messages.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// List of pending sends awaiting retry.
        /// </summary>
        private readonly List<PendingSend> _pending = new();

        /// <summary>
        /// Creates a new ReliableSendQueue with the given logger.
        /// </summary>
        public ReliableSendQueue(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>Number of entries currently waiting for retry.</summary>
        public int Count
        {
            get { return _pending.Count; }
        }

        /// <summary>
        ///     Enqueues a failed send for retry. The data is copied into a new buffer
        ///     so the caller's buffer can be reused immediately.
        ///     The first retry will be attempted on the next frame (NextRetryTime = 0).
        /// </summary>
        public void Enqueue(ConnectionId connectionId, int pipelineId, byte[] data, int offset, int length)
        {
            byte[] copy = new byte[length];
            System.Array.Copy(data, offset, copy, 0, length);

            _pending.Add(new PendingSend
            {
                ConnectionId = connectionId,
                PipelineId = pipelineId,
                Data = copy,
                Offset = 0,
                Length = length,
                RetryCount = 0,
                NextRetryTime = 0f,
            });
        }

        /// <summary>
        ///     Retries all buffered sends whose backoff delay has elapsed.
        ///     Removes entries that succeed or exceed <see cref="NetworkConstants.MaxSendRetries" />.
        ///     Returns the number of entries that were permanently dropped.
        /// </summary>
        public int Flush(INetworkTransport transport, float currentTime)
        {
            int dropped = 0;
            int i = 0;

            while (i < _pending.Count)
            {
                PendingSend entry = _pending[i];

                // Backoff: skip entries not yet due for retry
                if (currentTime < entry.NextRetryTime)
                {
                    i++;
                    continue;
                }

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

                // Exponential backoff: 0.1s, 0.2s, 0.4s for retries 1, 2, 3
                entry.NextRetryTime = currentTime
                    + NetworkConstants.RetryBackoffBaseSeconds * (1 << entry.RetryCount);
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
            /// <summary>Target connection for the retry.</summary>
            public ConnectionId ConnectionId;

            /// <summary>UTP pipeline index for the send.</summary>
            public int PipelineId;

            /// <summary>Copied payload bytes.</summary>
            public byte[] Data;

            /// <summary>Offset into Data.</summary>
            public int Offset;

            /// <summary>Payload length in bytes.</summary>
            public int Length;

            /// <summary>Number of failed retry attempts so far.</summary>
            public int RetryCount;

            /// <summary>
            ///     Realtime timestamp after which this entry may be retried.
            ///     Zero on initial enqueue (retry immediately next frame).
            ///     Set to currentTime + backoff delay on each failure.
            /// </summary>
            public float NextRetryTime;
        }
    }
}