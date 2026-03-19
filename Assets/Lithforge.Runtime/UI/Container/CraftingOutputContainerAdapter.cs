using Lithforge.Item;
using Lithforge.Item.Crafting;
using Lithforge.Runtime.Content.Tools;
using Lithforge.Voxel.Item;

namespace Lithforge.Runtime.UI.Container
{
    /// <summary>
    ///     Read-only container for the crafting output slot.
    ///     Shows the result of the current recipe match.
    ///     TakeOutput() consumes ingredients from the crafting grid.
    /// </summary>
    public sealed class CraftingOutputContainerAdapter : ISlotContainer
    {
        /// <summary>Item registry for looking up item definitions during output construction.</summary>
        private readonly ItemRegistry _itemRegistry;

        /// <summary>Tool template registry for constructing tool instances from recipe results.</summary>
        private readonly ToolTemplateRegistry _toolTemplateRegistry;

        /// <summary>The current item stack displayed in the output slot.</summary>
        private ItemStack _displayStack;

        /// <summary>Creates an output adapter with item and tool template registries for result construction.</summary>
        public CraftingOutputContainerAdapter(ItemRegistry itemRegistry, ToolTemplateRegistry toolTemplateRegistry)
        {
            _itemRegistry = itemRegistry;
            _toolTemplateRegistry = toolTemplateRegistry;
        }

        /// <summary>The currently matched recipe, or null if no match.</summary>
        public RecipeEntry CurrentMatch { get; private set; }

        /// <summary>Always returns 1 because the output is a single slot.</summary>
        public int SlotCount
        {
            get { return 1; }
        }

        /// <summary>Returns true because items cannot be placed into the output slot.</summary>
        public bool IsReadOnly
        {
            get { return true; }
        }

        /// <summary>Returns the current output item stack.</summary>
        public ItemStack GetSlot(int index)
        {
            return _displayStack;
        }

        /// <summary>No-op because the output slot is read-only.</summary>
        public void SetSlot(int index, ItemStack stack)
        {
            // Read-only — no external setting.
        }

        /// <summary>No-op because the output slot has no side effects on change.</summary>
        public void OnSlotChanged(int index)
        {
            // No side effects.
        }

        /// <summary>
        ///     Updates the displayed output based on a recipe match result.
        ///     Called by CraftingGridContainerAdapter.OnSlotChanged().
        /// </summary>
        public void SetRecipeMatch(RecipeEntry match)
        {
            CurrentMatch = match;

            if (match != null)
            {
                byte[] toolData = _toolTemplateRegistry?.GetTemplate(match.ResultItem);

                if (toolData != null)
                {
                    ToolInstance tool = ToolInstanceSerializer.Deserialize(toolData);
                    int durability = tool?.MaxDurability ?? -1;
                    _displayStack = new ItemStack(match.ResultItem, match.ResultCount, durability);
                    DataComponentMap toolMap = new();
                    toolMap.Set(DataComponentTypes.ToolInstanceId,
                        new ToolInstanceComponent(tool));
                    _displayStack.Components = toolMap;
                }
                else
                {
                    _displayStack = new ItemStack(match.ResultItem, match.ResultCount, -1);
                }
            }
            else
            {
                _displayStack = ItemStack.Empty;
            }
        }

        /// <summary>
        ///     Takes the output item and consumes one ingredient from each non-empty craft slot.
        ///     Returns the output ItemStack, or Empty if no match.
        /// </summary>
        public ItemStack TakeOutput(CraftingGridContainerAdapter gridAdapter)
        {
            if (CurrentMatch == null)
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
    }
}
