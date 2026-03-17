namespace Lithforge.Network.Messages
{
    public enum HandshakeRejectReason : byte
    {
        None = 0,
        ProtocolMismatch = 1,
        ContentMismatch = 2,
        ServerFull = 3,
        Banned = 4
    }
}
