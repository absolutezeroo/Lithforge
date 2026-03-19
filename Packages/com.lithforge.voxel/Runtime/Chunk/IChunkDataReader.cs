using Lithforge.Voxel.Block;

using Unity.Mathematics;

namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    ///     Minimal thread-safe read interface over chunk voxel data.
    ///     Implementations must be safe to call from background threads for all
    ///     player-adjacent chunks (those with positive reference counts), which is
    ///     guaranteed by <see cref="ChunkManager.AdjustRefCounts" />.
    ///     Unloaded or data-unavailable chunks return safe fallbacks (Air / false / 0).
    /// </summary>
    public interface IChunkDataReader
    {
        /// <summary>
        ///     Returns the <see cref="StateId" /> at the given world-space block coordinate.
        ///     Returns <see cref="StateId.Air" /> if the chunk is not loaded or data is unavailable.
        /// </summary>
        public StateId GetBlock(int3 worldCoord);

        /// <summary>
        ///     Returns true if the chunk containing the given coordinate is loaded
        ///     and has valid voxel data (state at least <see cref="ChunkState.RelightPending" />
        ///     with <c>Data.IsCreated</c>).
        /// </summary>
        public bool IsBlockLoaded(int3 worldCoord);

        /// <summary>
        ///     Returns the liquid cell byte at the given world-space block coordinate.
        ///     Returns 0 if the chunk is not loaded or has no liquid data.
        ///     Background-thread implementations must not call <c>JobHandle.Complete()</c>;
        ///     a stale-by-one-tick read is acceptable for swim detection.
        /// </summary>
        public byte GetFluidLevel(int3 worldCoord);
    }
}
