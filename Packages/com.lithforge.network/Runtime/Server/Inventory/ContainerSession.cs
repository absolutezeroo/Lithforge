using Lithforge.Item;

using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Represents an active container session between a player and a block entity.
    ///     Tracks the window ID, entity location, storage reference, per-session cursor,
    ///     and remote state for dirty-flag delta broadcasting.
    /// </summary>
    public sealed class ContainerSession
    {
        /// <summary>Assigned window ID for this session (>= 1).</summary>
        public byte WindowId;

        /// <summary>Player ID that owns this session.</summary>
        public ushort PlayerId;

        /// <summary>Chunk coordinate of the block entity.</summary>
        public int3 ChunkCoord;

        /// <summary>Flat index of the block within the chunk.</summary>
        public int FlatIndex;

        /// <summary>Block entity type identifier (e.g. "chest", "furnace").</summary>
        public string EntityTypeId;

        /// <summary>Reference to the block entity's item storage.</summary>
        public IItemStorage Storage;

        /// <summary>Per-client snapshot of last-sent slot states for delta detection.</summary>
        public ContainerRemoteState RemoteState;

        /// <summary>Per-session held item (cursor). Separate from the player inventory cursor.</summary>
        public ItemStack Cursor;

        /// <summary>Last burn progress value sent to this client (furnace only). 0-65535.</summary>
        public ushort LastSentBurnProgress;

        /// <summary>Last smelt progress value sent to this client (furnace only). 0-65535.</summary>
        public ushort LastSentSmeltProgress;
    }
}
