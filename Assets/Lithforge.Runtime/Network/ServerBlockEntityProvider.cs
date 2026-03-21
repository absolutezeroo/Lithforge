using Lithforge.Item;
using Lithforge.Network.Server;
using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.BlockEntity.Behaviors;
using Lithforge.Voxel.BlockEntity;
using Lithforge.Voxel.Chunk;

using Unity.Mathematics;

namespace Lithforge.Runtime.Network
{
    /// <summary>
    ///     Tier 3 implementation of <see cref="IServerBlockEntityProvider" />.
    ///     Uses <see cref="BlockEntityTickScheduler" /> for entity lookup and
    ///     <see cref="ChunkManager" /> for chunk access. Thread-safe: all accessed
    ///     data is either immutable or protected by InventoryBehavior._syncRoot.
    /// </summary>
    public sealed class ServerBlockEntityProvider : IServerBlockEntityProvider
    {
        /// <summary>Block entity tick scheduler with entity lookup by coord + flatIndex.</summary>
        private readonly BlockEntityTickScheduler _scheduler;

        /// <summary>Chunk manager for verifying chunk existence.</summary>
        private readonly ChunkManager _chunkManager;

        /// <summary>Creates a provider using the given scheduler and chunk manager.</summary>
        public ServerBlockEntityProvider(
            BlockEntityTickScheduler scheduler,
            ChunkManager chunkManager)
        {
            _scheduler = scheduler;
            _chunkManager = chunkManager;
        }

        /// <summary>Returns the item storage for a block entity, or null if none exists.</summary>
        public IItemStorage GetEntityInventory(int3 chunkCoord, int flatIndex)
        {
            BlockEntity.BlockEntity entity = _scheduler.GetEntity(chunkCoord, flatIndex);

            if (entity is null)
            {
                return null;
            }

            return entity switch
            {
                ChestBlockEntity chest => chest.Inventory,
                FurnaceBlockEntity furnace => furnace.Inventory,
                _ => null,
            };
        }

        /// <summary>Returns the type identifier of the block entity, or null.</summary>
        public string GetEntityTypeId(int3 chunkCoord, int flatIndex)
        {
            BlockEntity.BlockEntity entity = _scheduler.GetEntity(chunkCoord, flatIndex);
            return entity?.TypeId;
        }

        /// <summary>Returns true if a block entity exists at the given position.</summary>
        public bool EntityExists(int3 chunkCoord, int flatIndex)
        {
            return _scheduler.GetEntity(chunkCoord, flatIndex) is not null;
        }

        /// <summary>Returns the furnace burn progress (0.0–1.0), or 0 if no furnace.</summary>
        public float GetFurnaceBurnProgress(int3 chunkCoord, int flatIndex)
        {
            BlockEntity.BlockEntity entity = _scheduler.GetEntity(chunkCoord, flatIndex);

            if (entity is FurnaceBlockEntity furnace)
            {
                return furnace.FuelBurn.BurnProgress;
            }

            return 0f;
        }

        /// <summary>Returns the furnace smelt progress (0.0–1.0), or 0 if no furnace.</summary>
        public float GetFurnaceSmeltProgress(int3 chunkCoord, int flatIndex)
        {
            BlockEntity.BlockEntity entity = _scheduler.GetEntity(chunkCoord, flatIndex);

            if (entity is FurnaceBlockEntity furnace)
            {
                return furnace.Smelting.SmeltProgress;
            }

            return 0f;
        }
    }
}
