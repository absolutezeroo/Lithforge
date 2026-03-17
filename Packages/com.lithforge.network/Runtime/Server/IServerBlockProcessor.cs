using Lithforge.Voxel.Block;
using Lithforge.Voxel.Command;
using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    /// Bridge interface between <see cref="ServerGameLoop"/> (Tier 2) and the
    /// authoritative block mutation system (Tier 3). Validates server-side block
    /// command checks (rate limit, reach, chunk loaded, block type, position,
    /// break time, sequence, placement face) and calls ChunkManager.SetBlock
    /// on accepted commands. Implemented by <c>ServerBlockProcessor</c> in
    /// Lithforge.Runtime.Simulation.
    /// </summary>
    public interface IServerBlockProcessor
    {
        /// <summary>
        /// Initializes per-player rate limiting and digging state.
        /// Called when a player joins the server.
        /// </summary>
        void AddPlayer(ushort playerId);

        /// <summary>
        /// Cleans up per-player state (rate limit tokens, digging state).
        /// Called when a player disconnects.
        /// </summary>
        void RemovePlayer(ushort playerId);

        /// <summary>
        /// Refills the per-player rate-limit token bucket for the current tick.
        /// Must be called once per tick before any TryBreakBlock/TryPlaceBlock calls.
        /// </summary>
        void RefillRateLimitTokens(ushort playerId, float currentTime);

        /// <summary>
        /// Records that the player began mining at <paramref name="position"/>.
        /// Performs chunk-ready and reach checks; computes expected break time from hardness.
        /// Returns false if the position is invalid or unreachable.
        /// </summary>
        bool StartDigging(
            ushort playerId,
            int3 position,
            float3 playerPosition,
            uint serverTick);

        /// <summary>
        /// Cancels any in-progress dig for the player.
        /// Call on disconnect, target change, or player death.
        /// </summary>
        void CancelDigging(ushort playerId);

        /// <summary>
        /// Validates all server-side checks and breaks the block if accepted.
        /// Returns <see cref="BlockProcessResult"/> with the outcome and authoritative state.
        /// </summary>
        BlockProcessResult TryBreakBlock(
            ushort playerId,
            int3 position,
            float3 playerPosition,
            uint serverTick);

        /// <summary>
        /// Validates all server-side checks and places the block if accepted.
        /// Returns <see cref="BlockProcessResult"/> with the outcome and authoritative state.
        /// </summary>
        BlockProcessResult TryPlaceBlock(
            ushort playerId,
            int3 position,
            StateId blockState,
            BlockFace face,
            float3 playerPosition);

        /// <summary>
        /// Returns the current block state at the given world position.
        /// Returns <see cref="StateId.Air"/> if the chunk is not loaded.
        /// </summary>
        StateId GetBlock(int3 position);
    }
}
