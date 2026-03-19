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
        /// <summary>The backing inventory behavior being adapted.</summary>
        private readonly InventoryBehavior _inventory;

        /// <summary>Offset into the inventory for sub-range adapters.</summary>
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

        /// <summary>Number of slots exposed by this adapter.</summary>
        public int SlotCount { get; }

        /// <summary>Whether this adapter prevents item placement.</summary>
        public bool IsReadOnly { get; }

        /// <summary>Gets the item stack at the given local index, offset by the slot offset.</summary>
        public ItemStack GetSlot(int index)
        {
            return _inventory.GetSlot(_slotOffset + index);
        }

        /// <summary>Sets the item stack at the given local index, offset by the slot offset.</summary>
        public void SetSlot(int index, ItemStack stack)
        {
            _inventory.SetSlot(_slotOffset + index, stack);
        }

        /// <summary>No-op; block entity inventories have no side effects on slot change.</summary>
        public void OnSlotChanged(int index)
        {
            // No side effects needed for block entity inventory changes.
        }
    }
}
