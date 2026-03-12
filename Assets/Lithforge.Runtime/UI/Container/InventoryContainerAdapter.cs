using Lithforge.Voxel.Item;

namespace Lithforge.Runtime.UI.Container
{
    /// <summary>
    /// Adapts a slice of the player Inventory to the ISlotContainer interface.
    /// Supports offsetting into the inventory (e.g. hotbar = 0..8, main = 9..35).
    /// </summary>
    public sealed class InventoryContainerAdapter : ISlotContainer
    {
        private readonly Inventory _inventory;
        private readonly int _startSlot;
        private readonly int _slotCount;

        public InventoryContainerAdapter(Inventory inventory, int startSlot, int slotCount)
        {
            _inventory = inventory;
            _startSlot = startSlot;
            _slotCount = slotCount;
        }

        public int SlotCount
        {
            get { return _slotCount; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public ItemStack GetSlot(int index)
        {
            return _inventory.GetSlot(_startSlot + index);
        }

        public void SetSlot(int index, ItemStack stack)
        {
            _inventory.SetSlot(_startSlot + index, stack);
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
            return _startSlot + localIndex;
        }

        /// <summary>
        /// The start slot offset in the backing inventory.
        /// </summary>
        public int StartSlot
        {
            get { return _startSlot; }
        }
    }
}
