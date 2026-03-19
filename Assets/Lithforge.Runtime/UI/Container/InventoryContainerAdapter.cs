using Lithforge.Item;

namespace Lithforge.Runtime.UI.Container
{
    /// <summary>
    /// Adapts a slice of the player Inventory to the ISlotContainer interface.
    /// Supports offsetting into the inventory (e.g. hotbar = 0..8, main = 9..35).
    /// </summary>
    public sealed class InventoryContainerAdapter : ISlotContainer
    {
        private readonly Inventory _inventory;

        public InventoryContainerAdapter(Inventory inventory, int startSlot, int slotCount)
        {
            _inventory = inventory;
            StartSlot = startSlot;
            SlotCount = slotCount;
        }

        public int SlotCount { get; }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public ItemStack GetSlot(int index)
        {
            return _inventory.GetSlot(StartSlot + index);
        }

        public void SetSlot(int index, ItemStack stack)
        {
            _inventory.SetSlot(StartSlot + index, stack);
        }

        public void OnSlotChanged(int index)
        {
            // No side effects for plain inventory slots.
        }

        /// <summary>
        /// Returns the absolute inventory index for a local adapter index.
        /// Used by shift-click transfer to determine hotbar vs main.
        /// </summary>
        public int ToAbsoluteIndex(int localIndex)
        {
            return StartSlot + localIndex;
        }

        /// <summary>
        /// The start slot offset in the backing inventory.
        /// </summary>
        public int StartSlot { get; }
    }
}
