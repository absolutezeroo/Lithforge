namespace Lithforge.Network
{
    /// <summary>
    ///     Provides per-frame network counters for metrics sampling.
    ///     Implemented by <see cref="Server.NetworkServer" /> (server-side)
    ///     and <see cref="Client.NetworkClient" /> (client-side).
    ///     The metrics system calls <see cref="SampleAndReset" /> once per frame
    ///     after reading all counter values.
    /// </summary>
    public interface INetworkMetricsSource
    {
        /// <summary>Bytes sent over the network since the last <see cref="SampleAndReset" /> call.</summary>
        public int BytesSent { get; }

        /// <summary>Bytes received from the network since the last <see cref="SampleAndReset" /> call.</summary>
        public int BytesReceived { get; }

        /// <summary>Network messages sent since the last <see cref="SampleAndReset" /> call.</summary>
        public int MessagesSent { get; }

        /// <summary>Network messages received since the last <see cref="SampleAndReset" /> call.</summary>
        public int MessagesReceived { get; }

        /// <summary>Entries currently in the reliable send retry queue.</summary>
        public int PendingReliableQueueCount { get; }

        /// <summary>Number of active connected peers.</summary>
        public int PeerCount { get; }

        /// <summary>Average round-trip time to peers in milliseconds. 0 if not applicable.</summary>
        public float AveragePingMs { get; }

        /// <summary>Resets per-frame byte and message counters to zero.</summary>
        public void SampleAndReset();
    }
}
