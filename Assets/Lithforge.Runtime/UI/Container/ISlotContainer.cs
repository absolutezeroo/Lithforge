using Lithforge.Voxel.Item;

namespace Lithforge.Runtime.UI.Container
{
    /// <summary>
    /// Abstraction over any container of item slots (inventory, crafting grid, output).
    /// Screen-agnostic — allows the interaction controller to operate on any container type.
    /// </summary>
    public interface ISlotContainer
    {
        /// <summary>Total number of slots in this container.</summary>
        public int SlotCount { get; }

        /// <summary>True if items cannot be placed into this container (e.g. craft output).</summary>
        public bool IsReadOnly { get; }

        /// <summary>Gets the item stack at the given slot index.</summary>
        public ItemStack GetSlot(int index);

        /// <summary>Sets the item stack at the given slot index.</summary>
        public void SetSlot(int index, ItemStack stack);

        /// <summary>
        /// Called after a slot is modified. Implementations can trigger side effects
        /// like rechecking crafting recipes.
        /// </summary>
        public void OnSlotChanged(int index);
    }
}
