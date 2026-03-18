namespace Lithforge.Network.Messages
{
    public enum DisconnectReason : byte
    {
        Graceful = 0,
        Timeout = 1,
        Kicked = 2,
        ServerShutdown = 3,
    }
}
