using Lithforge.Item;

namespace Lithforge.Runtime.UI.Interaction
{
    /// <summary>
    /// Holds the item stack currently attached to the cursor during inventory interaction.
    /// Wraps mutation so drag ghost and tooltip can react to changes.
    /// </summary>
    public sealed class HeldStack
    {
        private ItemStack _stack;

        public ItemStack Stack
        {
            get { return _stack; }
        }

        public bool IsEmpty
        {
            get { return _stack.IsEmpty; }
        }

        public void Set(ItemStack stack)
        {
            _stack = stack;
        }

        public void Clear()
        {
            _stack = ItemStack.Empty;
        }
    }
}
