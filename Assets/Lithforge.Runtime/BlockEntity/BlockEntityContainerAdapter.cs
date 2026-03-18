using Lithforge.Runtime.BlockEntity.Behaviors;
using Lithforge.Runtime.UI.Container;
using Lithforge.Item;

namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    ///     Adapts an InventoryBehavior to the ISlotContainer interface,
    ///     allowing block entity inventories to be used with the existing
    ///     SlotInteractionController and ContainerScreen infrastructure.
    /// </summary>
    public sealed class BlockEntityContainerAdapter : ISlotContainer
    {
        private readonly InventoryBehavior _inventory;

        private readonly int _slotOffset;

        /// <summary>
        ///     Wraps the full inventory (all slots).
        /// </summary>
        public BlockEntityContainerAdapter(InventoryBehavior inventory, bool isReadOnly = false)
        {
            _inventory = inventory;
            _slotOffset = 0;
            SlotCount = inventory.SlotCount;
            IsReadOnly = isReadOnly;
        }

        /// <summary>
        ///     Wraps a sub-range of the inventory (slotOffset .. slotOffset+slotCount-1).
        ///     Used by FurnaceScreen to expose one slot per adapter.
        /// </summary>
        public BlockEntityContainerAdapter(
            InventoryBehavior inventory, int slotOffset, int slotCount, bool isReadOnly = false)
        {
            _inventory = inventory;
            _slotOffset = slotOffset;
            SlotCount = slotCount;
            IsReadOnly = isReadOnly;
        }

        public int SlotCount { get; }

        public bool IsReadOnly { get; }

        public ItemStack GetSlot(int index)
        {
            return _inventory.GetSlot(_slotOffset + index);
        }

        public void SetSlot(int index, ItemStack stack)
        {
            _inventory.SetSlot(_slotOffset + index, stack);
        }

        public void OnSlotChanged(int index)
        {
            // No side effects needed for block entity inventory changes.
        }
    }
}
