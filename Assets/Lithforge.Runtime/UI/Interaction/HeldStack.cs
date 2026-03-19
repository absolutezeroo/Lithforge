using Lithforge.Item;

namespace Lithforge.Runtime.UI.Interaction
{
    /// <summary>
    /// Holds the item stack currently attached to the cursor during inventory interaction.
    /// Wraps mutation so drag ghost and tooltip can react to changes.
    /// </summary>
    public sealed class HeldStack
    {
        /// <summary>The item stack currently held by the cursor.</summary>
        private ItemStack _stack;

        /// <summary>Gets the current held item stack.</summary>
        public ItemStack Stack
        {
            get { return _stack; }
        }

        /// <summary>True if the cursor is not holding any items.</summary>
        public bool IsEmpty
        {
            get { return _stack.IsEmpty; }
        }

        /// <summary>Replaces the held stack with the given stack.</summary>
        public void Set(ItemStack stack)
        {
            _stack = stack;
        }

        /// <summary>Sets the held stack to empty, releasing any held items.</summary>
        public void Clear()
        {
            _stack = ItemStack.Empty;
        }
    }
}
