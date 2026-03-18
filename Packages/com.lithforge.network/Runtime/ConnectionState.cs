namespace Lithforge.Network
{
    public enum ConnectionState : byte
    {
        Disconnected = 0,
        Connecting = 1,
        Handshaking = 2,
        Loading = 3,
        Playing = 4,
        Disconnecting = 5,
    }
}
