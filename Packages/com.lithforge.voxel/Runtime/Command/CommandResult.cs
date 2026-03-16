namespace Lithforge.Voxel.Command
{
    /// <summary>
    /// Result of command processing. Exposes failure reason for debug logging
    /// and future server rejection packet construction.
    /// </summary>
    public enum CommandResult : byte
    {
        /// <summary>Command was accepted and applied.</summary>
        Success = 0,

        /// <summary>Target block does not exist (is air when it should not be).</summary>
        BlockNotFound = 1,

        /// <summary>Placement target is already occupied.</summary>
        TargetOccupied = 2,

        /// <summary>Placed block would intersect a player AABB.</summary>
        PlayerOverlap = 3,

        /// <summary>Target position is outside interaction range.</summary>
        OutOfRange = 4,

        /// <summary>Player has no item in hand for placement.</summary>
        NoItemInHand = 5,

        /// <summary>Held item is not a block item.</summary>
        NotABlockItem = 6,

        /// <summary>Target chunk is not in a ready state.</summary>
        ChunkNotReady = 7,

        /// <summary>Command sequence ID is not strictly greater than last processed.</summary>
        RateLimited = 8,

        /// <summary>Block cannot be broken (hardness is infinite or unbreakable).</summary>
        NotBreakable = 9,
    }
}
