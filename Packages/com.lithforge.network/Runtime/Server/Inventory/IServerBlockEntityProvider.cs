using Lithforge.Item;

using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Abstraction for server-side access to block entity inventories.
    ///     The server thread (Tier 2) calls this interface to read/write block entity
    ///     slots during container session processing. Implemented in Tier 3 by
    ///     ServerBlockEntityProvider using ChunkManager and ManagedChunk.BlockEntities.
    /// </summary>
    public interface IServerBlockEntityProvider
    {
        /// <summary>
        ///     Returns the item storage for a block entity at the given chunk position
        ///     and flat index, or null if no entity exists there.
        /// </summary>
        public IItemStorage GetEntityInventory(int3 chunkCoord, int flatIndex);

        /// <summary>
        ///     Returns the type identifier of the block entity at the given position,
        ///     or null if no entity exists there.
        /// </summary>
        public string GetEntityTypeId(int3 chunkCoord, int flatIndex);

        /// <summary>Returns true if a block entity exists at the given position.</summary>
        public bool EntityExists(int3 chunkCoord, int flatIndex);

        /// <summary>
        ///     Returns the furnace burn progress (0.0–1.0) at the given position,
        ///     or 0 if no furnace exists there.
        /// </summary>
        public float GetFurnaceBurnProgress(int3 chunkCoord, int flatIndex);

        /// <summary>
        ///     Returns the furnace smelt progress (0.0–1.0) at the given position,
        ///     or 0 if no furnace exists there.
        /// </summary>
        public float GetFurnaceSmeltProgress(int3 chunkCoord, int flatIndex);
    }
}
