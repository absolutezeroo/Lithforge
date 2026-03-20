namespace Lithforge.Network.Message
{
    /// <summary>
    ///     Byte-sized message type identifiers. Grouped by category:
    ///     1-9: Connection lifecycle, 10-19: Keepalive, 20-39: Client→Server commands,
    ///     40-59: Server→Client state updates.
    /// </summary>
    public enum MessageType : byte
    {
        // Connection lifecycle

        /// <summary>Client requests connection with protocol version and content hash.</summary>
        HandshakeRequest = 1,

        /// <summary>Server accepts or rejects the handshake.</summary>
        HandshakeResponse = 2,

        /// <summary>Either side notifies graceful disconnection with a reason code.</summary>
        Disconnect = 3,

        /// <summary>Server sends a challenge nonce for client identity verification.</summary>
        HandshakeChallenge = 4,

        /// <summary>Client responds with a signature proving ownership of the public key.</summary>
        ChallengeResponse = 5,

        // Keepalive

        /// <summary>Client or server sends a ping for RTT measurement.</summary>
        Ping = 10,

        /// <summary>Reply to a Ping, echoing the sequence number.</summary>
        Pong = 11,

        // Client-to-Server gameplay commands

        /// <summary>Client sends movement input (yaw, pitch, flags) each tick.</summary>
        MoveInput = 20,

        /// <summary>Client requests placing a block at a world position.</summary>
        PlaceBlockCmd = 21,

        /// <summary>Client requests breaking a block at a world position.</summary>
        BreakBlockCmd = 22,

        /// <summary>Client requests an interaction with a block or entity.</summary>
        InteractCmd = 23,

        /// <summary>Client requests a slot click in an open container.</summary>
        SlotClickCmd = 24,

        /// <summary>Client notifies the server that mining has started at a position.</summary>
        StartDiggingCmd = 25,

        /// <summary>Client signals readiness after receiving initial world state.</summary>
        ClientReady = 26,

        /// <summary>Client acknowledges receipt of a batch of chunk data messages.</summary>
        ChunkBatchAck = 27,

        // Server-to-Client state updates

        /// <summary>Server sends authoritative player position and physics state.</summary>
        PlayerState = 40,

        /// <summary>Server notifies a single block change.</summary>
        BlockChange = 41,

        /// <summary>Server notifies multiple block changes in a single chunk.</summary>
        MultiBlockChange = 42,

        /// <summary>Server sends full chunk voxel data (compressed).</summary>
        ChunkData = 43,

        /// <summary>Server tells client to unload a chunk that left its interest area.</summary>
        ChunkUnload = 44,

        /// <summary>Server signals that the game is ready (spawn position, time of day, seed).</summary>
        GameReady = 45,

        /// <summary>Server acknowledges or rejects a client block command.</summary>
        AcknowledgeBlockChange = 46,

        /// <summary>Server notifies client to spawn a remote player entity.</summary>
        SpawnPlayer = 47,

        /// <summary>Server notifies client to despawn a remote player entity.</summary>
        DespawnPlayer = 48,

        /// <summary>Server sends initial spawn data before GameReady (player ID, spawn position).</summary>
        SpawnInit = 49,

        /// <summary>Server sends a full inventory snapshot to a client.</summary>
        InventorySync = 51,

        /// <summary>Server sends a targeted single-slot correction to a client.</summary>
        InventorySlotUpdate = 52,
    }
}
