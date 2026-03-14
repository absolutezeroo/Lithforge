using Lithforge.Runtime.BlockEntity.Behaviors;
using Lithforge.Runtime.UI.Container;
using Lithforge.Voxel.Item;

namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    /// Adapts an InventoryBehavior to the ISlotContainer interface,
    /// allowing block entity inventories to be used with the existing
    /// SlotInteractionController and ContainerScreen infrastructure.
    /// </summary>
    public sealed class BlockEntityContainerAdapter : ISlotContainer
    {
        private readonly InventoryBehavior _inventory;
        private readonly bool _isReadOnly;

        public int SlotCount
        {
            get { return _inventory.SlotCount; }
        }

        public bool IsReadOnly
        {
            get { return _isReadOnly; }
        }

        public BlockEntityContainerAdapter(InventoryBehavior inventory, bool isReadOnly = false)
        {
            _inventory = inventory;
            _isReadOnly = isReadOnly;
        }

        public ItemStack GetSlot(int index)
        {
            return _inventory.GetSlot(index);
        }

        public void SetSlot(int index, ItemStack stack)
        {
            _inventory.SetSlot(index, stack);
        }

        public void OnSlotChanged(int index)
        {
            // No side effects needed for block entity inventory changes.
        }
    }
}
