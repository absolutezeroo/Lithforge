using Lithforge.Core.Data;
using Lithforge.Runtime.Content.Tools;
using Lithforge.Voxel.Crafting;
using Lithforge.Voxel.Item;

namespace Lithforge.Runtime.UI.Container
{
    /// <summary>
    /// Read-only container for the crafting output slot.
    /// Shows the result of the current recipe match.
    /// TakeOutput() consumes ingredients from the crafting grid.
    /// </summary>
    public sealed class CraftingOutputContainerAdapter : ISlotContainer
    {
        private readonly ItemRegistry _itemRegistry;
        private RecipeEntry _currentMatch;
        private ItemStack _displayStack;

        public CraftingOutputContainerAdapter(ItemRegistry itemRegistry)
        {
            _itemRegistry = itemRegistry;
        }

        public int SlotCount
        {
            get { return 1; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public ItemStack GetSlot(int index)
        {
            return _displayStack;
        }

        public void SetSlot(int index, ItemStack stack)
        {
            // Read-only — no external setting.
        }

        public void OnSlotChanged(int index)
        {
            // No side effects.
        }

        /// <summary>
        /// Updates the displayed output based on a recipe match result.
        /// Called by CraftingGridContainerAdapter.OnSlotChanged().
        /// </summary>
        public void SetRecipeMatch(RecipeEntry match)
        {
            _currentMatch = match;

            if (match != null)
            {
                ItemEntry resultDef = _itemRegistry.Get(match.ResultItem);
                int durability = (resultDef != null && resultDef.Durability > 0)
                    ? resultDef.Durability
                    : -1;
                _displayStack = new ItemStack(match.ResultItem, match.ResultCount, durability);
                byte[] toolData = ToolTemplateRegistry.GetTemplate(match.ResultItem);

                if (toolData != null)
                {
                    _displayStack.CustomData = toolData;
                }
            }
            else
            {
                _displayStack = ItemStack.Empty;
            }
        }

        /// <summary>
        /// Takes the output item and consumes one ingredient from each non-empty craft slot.
        /// Returns the output ItemStack, or Empty if no match.
        /// </summary>
        public ItemStack TakeOutput(CraftingGridContainerAdapter gridAdapter)
        {
            if (_currentMatch == null)
            {
                return ItemStack.Empty;
            }

            ItemStack result = _displayStack;

            // Consume one item from each non-empty craft grid slot
            CraftingGrid grid = gridAdapter.Grid;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    ItemStack gridStack = grid.GetSlotStack(x, y);

                    if (!gridStack.IsEmpty)
                    {
                        ItemStack consumed = gridStack;
                        consumed.Count -= 1;
                        grid.SetSlotStack(x, y, consumed.IsEmpty ? ItemStack.Empty : consumed);
                    }
                }
            }

            // Recheck recipe after consumption
            gridAdapter.OnSlotChanged(0);

            return result;
        }

        public RecipeEntry CurrentMatch
        {
            get { return _currentMatch; }
        }
    }
}
