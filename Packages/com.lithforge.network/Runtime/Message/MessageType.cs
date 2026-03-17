namespace Lithforge.Network.Message
{
    /// <summary>
    /// Byte-sized message type identifiers. Grouped by category:
    /// 1-9: Connection lifecycle, 10-19: Keepalive, 20-39: Client→Server commands,
    /// 40-59: Server→Client state updates.
    /// </summary>
    public enum MessageType : byte
    {
        // Connection lifecycle
        HandshakeRequest = 1,
        HandshakeResponse = 2,
        Disconnect = 3,

        // Keepalive
        Ping = 10,
        Pong = 11,

        // Client→Server gameplay commands (reserved for P3/P4/P5)
        MoveInput = 20,
        PlaceBlockCmd = 21,
        BreakBlockCmd = 22,
        InteractCmd = 23,
        SlotClickCmd = 24,

        // Server→Client state updates (reserved for P3/P4/P5)
        PlayerState = 40,
        BlockChange = 41,
        MultiBlockChange = 42,
        ChunkData = 43,
        ChunkUnload = 44,
    }
}
