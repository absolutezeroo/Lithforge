namespace Lithforge.Item
{
    /// <summary>
    ///     Uniform read/write interface for item slot storage. Implemented by
    ///     <see cref="Inventory" /> (player), <see cref="Crafting.CraftingGrid" />
    ///     (crafting), and block entity inventories (InventoryBehavior).
    ///     Used by the server to route slot clicks across different container types.
    /// </summary>
    public interface IItemStorage
    {
        /// <summary>Total number of item slots in this storage.</summary>
        public int SlotCount { get; }

        /// <summary>Returns the ItemStack at the given slot index.</summary>
        public ItemStack GetSlot(int index);

        /// <summary>Sets the ItemStack at the given slot index.</summary>
        public void SetSlot(int index, ItemStack stack);
    }
}
