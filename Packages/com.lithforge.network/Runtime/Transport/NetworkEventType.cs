namespace Lithforge.Network.Transport
{
    public enum NetworkEventType : byte
    {
        Empty = 0,
        Connect = 1,
        Data = 2,
        Disconnect = 3,
    }
}
