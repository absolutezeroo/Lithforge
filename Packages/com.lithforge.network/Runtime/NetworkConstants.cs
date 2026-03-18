namespace Lithforge.Network
{
    public static class NetworkConstants
    {
        public const ushort DefaultPort = 25565;
        public const ushort ProtocolVersion = 1;
        public const int MaxConnections = 200;
        public const float HandshakeTimeoutSeconds = 10f;
        public const float IdleTimeoutSeconds = 30f;
        public const float LoadingTimeoutSeconds = 120f;
        public const float PingIntervalSeconds = 5f;
        public const int SendQueueFullError = -5;
        public const int MaxSendRetries = 3;
        public const int SendQueueCapacity = 4096;
        public const int ReceiveQueueCapacity = 4096;
        public const int DisconnectTimeoutMs = 30000;
        public const int HeartbeatTimeoutMs = 500;
        public const int ReliableWindowSize = 64;
        public const int FragmentationPayloadCapacity = 65536;
        public const int MaxPlayerNameLength = 32;
    }
}
