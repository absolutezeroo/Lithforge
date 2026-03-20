namespace Lithforge.Network
{
    /// <summary>
    /// Compile-time network configuration constants shared by server and client.
    /// </summary>
    public static class NetworkConstants
    {
        /// <summary>
        /// Default server listen port.
        /// </summary>
        public const ushort DefaultPort = 25565;

        /// <summary>
        /// Current network protocol version. Clients must match to connect.
        /// </summary>
        public const ushort ProtocolVersion = 2;

        /// <summary>
        /// Maximum number of concurrent connections the server accepts.
        /// </summary>
        public const int MaxConnections = 200;

        /// <summary>
        /// Maximum seconds allowed for the handshake exchange before disconnecting.
        /// </summary>
        public const float HandshakeTimeoutSeconds = 10f;

        /// <summary>
        /// Maximum seconds a connection may be idle before being timed out.
        /// </summary>
        public const float IdleTimeoutSeconds = 30f;

        /// <summary>
        /// Maximum seconds a peer may remain in Loading state before being force-transitioned.
        /// </summary>
        public const float LoadingTimeoutSeconds = 120f;

        /// <summary>
        /// Interval in seconds between ping/pong keepalive messages.
        /// </summary>
        public const float PingIntervalSeconds = 5f;

        /// <summary>
        /// UTP error code returned when the send queue is full.
        /// </summary>
        public const int SendQueueFullError = -5;
        /// <summary>
        ///     Attempts before a queued send is permanently dropped (retry indices 0..MaxSendRetries-1).
        ///     With exponential backoff, this gives 4 attempts over ~0.7 seconds total.
        /// </summary>
        public const int MaxSendRetries = 4;

        /// <summary>Base delay in seconds for the first retry backoff. Doubles each attempt.</summary>
        public const float RetryBackoffBaseSeconds = 0.1f;

        /// <summary>Seconds before an unacknowledged block prediction is reverted.</summary>
        public const float PredictionExpirySeconds = 5f;
        /// <summary>
        /// UTP driver send queue capacity in messages.
        /// </summary>
        public const int SendQueueCapacity = 4096;

        /// <summary>
        /// UTP driver receive queue capacity in messages.
        /// </summary>
        public const int ReceiveQueueCapacity = 4096;

        /// <summary>
        /// UTP disconnect timeout in milliseconds.
        /// </summary>
        public const int DisconnectTimeoutMs = 30000;

        /// <summary>
        /// UTP heartbeat timeout in milliseconds.
        /// </summary>
        public const int HeartbeatTimeoutMs = 500;

        /// <summary>
        /// Reliable pipeline window size (max unacknowledged packets).
        /// </summary>
        public const int ReliableWindowSize = 64;

        /// <summary>
        /// Maximum payload capacity for the fragmented reliable pipeline.
        /// </summary>
        public const int FragmentationPayloadCapacity = 65536;

        /// <summary>
        /// Maximum UTF-8 byte length for player names.
        /// </summary>
        public const int MaxPlayerNameLength = 32;

        /// <summary>Maximum UTF-8 byte length for player UUIDs.</summary>
        public const int MaxUuidLength = 36;

        /// <summary>Maximum UTF-8 byte length for chat messages.</summary>
        public const int MaxChatLength = 256;

        /// <summary>
        /// Maximum chunks in-flight (sent but not ACK'd) per peer before the server
        /// pauses streaming. Prevents overwhelming slow clients. Clients send
        /// <see cref="Messages.ChunkBatchAckMessage"/> to release window slots.
        /// </summary>
        public const int MaxInFlightChunks = 32;

        /// <summary>
        /// Number of chunk receipts the client accumulates before sending
        /// a <see cref="Messages.ChunkBatchAckMessage"/> back to the server.
        /// Lower values give tighter flow control; higher values reduce ACK traffic.
        /// </summary>
        public const int ChunkAckBatchSize = 4;
    }
}
