using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    /// Tracks per-player active mining state on the server. When a
    /// <see cref="Lithforge.Network.Messages.StartDiggingCmdMessage"/> is received,
    /// the server records the block position and tick. On
    /// <see cref="Lithforge.Network.Messages.BreakBlockCmdMessage"/>, it validates
    /// that enough time elapsed and that the position matches.
    /// </summary>
    public struct PlayerDiggingState
    {
        /// <summary>Whether the player is currently mining a block.</summary>
        public bool IsDigging;

        /// <summary>World-space coordinate of the block being mined.</summary>
        public int3 DigPosition;

        /// <summary>Server tick when StartDigging was received.</summary>
        public uint DigStartTick;

        /// <summary>
        /// Expected break time in seconds, computed from block hardness at the
        /// hand-mining rate (worst case). The server accepts breaks at 50% of
        /// this value to account for tools and network jitter.
        /// </summary>
        public float ExpectedBreakTime;
    }
}
