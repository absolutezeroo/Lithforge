using Lithforge.Item;

namespace Lithforge.Runtime.UI.Container
{
    /// <summary>
    ///     Abstraction over any container of item slots (inventory, crafting grid, output).
    ///     Screen-agnostic — allows the interaction controller to operate on any container type.
    ///     Extends <see cref="IItemStorage" /> so containers can be passed directly to
    ///     <see cref="Lithforge.Item.Interaction.SlotActionExecutor" /> for mutation.
    /// </summary>
    public interface ISlotContainer : IItemStorage
    {
        /// <summary>True if items cannot be placed into this container (e.g. craft output).</summary>
        public bool IsReadOnly { get; }

        /// <summary>
        ///     Called after a slot is modified. Implementations can trigger side effects
        ///     like rechecking crafting recipes.
        /// </summary>
        public void OnSlotChanged(int index);
    }
}
