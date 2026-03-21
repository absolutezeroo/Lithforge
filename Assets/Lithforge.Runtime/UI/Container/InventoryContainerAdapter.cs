using Lithforge.Item;

namespace Lithforge.Runtime.UI.Container
{
    /// <summary>
    /// Adapts a slice of the player Inventory to the ISlotContainer interface.
    /// Supports offsetting into the inventory (e.g. hotbar = 0..8, main = 9..35).
    /// </summary>
    public sealed class InventoryContainerAdapter : ISlotContainer
    {
        /// <summary>The backing player inventory being adapted.</summary>
        private readonly Inventory _inventory;

        /// <summary>The backing player inventory. Exposed for delegation to SlotActionExecutor.</summary>
        public Inventory Inventory
        {
            get { return _inventory; }
        }

        /// <summary>Creates an adapter wrapping a slice of the inventory from startSlot for slotCount slots.</summary>
        public InventoryContainerAdapter(Inventory inventory, int startSlot, int slotCount)
        {
            _inventory = inventory;
            StartSlot = startSlot;
            SlotCount = slotCount;
        }

        /// <summary>Number of slots exposed by this adapter.</summary>
        public int SlotCount { get; }

        /// <summary>Returns false because inventory slots accept item placement.</summary>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>Gets the item stack at the given local slot index, offset by StartSlot.</summary>
        public ItemStack GetSlot(int index)
        {
            return _inventory.GetSlot(StartSlot + index);
        }

        /// <summary>Sets the item stack at the given local slot index, offset by StartSlot.</summary>
        public void SetSlot(int index, ItemStack stack)
        {
            _inventory.SetSlot(StartSlot + index, stack);
        }

        /// <summary>No-op for plain inventory slots; no side effects needed on change.</summary>
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
