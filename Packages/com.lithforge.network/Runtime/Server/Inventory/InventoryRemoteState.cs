using Lithforge.Item;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Snapshot of inventory state last confirmed sent to a specific client.
    ///     Compared against the live Inventory each Phase 5 tick to detect dirty slots.
    ///     Pre-allocated once per player at accept time; never replaced.
    /// </summary>
    internal sealed class InventoryRemoteState
    {
        /// <summary>Pre-allocated snapshot array matching Inventory.SlotCount.</summary>
        private readonly ItemStack[] _remoteSlots = new ItemStack[Inventory.SlotCount];

        /// <summary>Last-sent cursor state.</summary>
        private ItemStack _remoteCursor;

        /// <summary>Returns true if the given slot differs from the remote snapshot.</summary>
        public bool IsSlotDirty(int index, ItemStack current)
        {
            return _remoteSlots[index] != current;
        }

        /// <summary>Returns true if the cursor differs from the remote cursor snapshot.</summary>
        public bool IsCursorDirty(ItemStack current)
        {
            return _remoteCursor != current;
        }

        /// <summary>Updates the snapshot for a single slot after sending it to the client.</summary>
        public void MarkSlotSent(int index, ItemStack value)
        {
            _remoteSlots[index] = value;
        }

        /// <summary>Updates the cursor snapshot after sending it to the client.</summary>
        public void MarkCursorSent(ItemStack cursor)
        {
            _remoteCursor = cursor;
        }

        /// <summary>Updates all slots and cursor from the current inventory state (used after full sync).</summary>
        public void SyncAll(Inventory inventory, ItemStack cursor)
        {
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                _remoteSlots[i] = inventory.GetSlot(i);
            }

            _remoteCursor = cursor;
        }
    }
}
