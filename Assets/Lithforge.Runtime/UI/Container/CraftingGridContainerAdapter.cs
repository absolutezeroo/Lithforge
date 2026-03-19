using Lithforge.Item.Crafting;
using Lithforge.Item;

namespace Lithforge.Runtime.UI.Container
{
    /// <summary>
    /// Adapts a CraftingGrid to the ISlotContainer interface.
    /// Linearizes the 2D grid (row-major: index = y * width + x).
    /// Calls CraftingEngine.FindMatch on slot changes to update the output.
    /// </summary>
    public sealed class CraftingGridContainerAdapter : ISlotContainer
    {
        private readonly CraftingEngine _engine;
        private readonly CraftingOutputContainerAdapter _output;

        public CraftingGridContainerAdapter(
            CraftingGrid grid,
            CraftingEngine engine,
            CraftingOutputContainerAdapter output)
        {
            Grid = grid;
            _engine = engine;
            _output = output;
        }

        public int SlotCount
        {
            get { return Grid.Width * Grid.Height; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public ItemStack GetSlot(int index)
        {
            int x = index % Grid.Width;
            int y = index / Grid.Width;
            return Grid.GetSlotStack(x, y);
        }

        public void SetSlot(int index, ItemStack stack)
        {
            int x = index % Grid.Width;
            int y = index / Grid.Width;
            Grid.SetSlotStack(x, y, stack);
        }

        public void OnSlotChanged(int index)
        {
            // Recheck recipe match whenever a craft slot changes
            RecipeEntry match = _engine.FindMatch(Grid);
            _output.SetRecipeMatch(match);
        }

        public CraftingGrid Grid { get; }
    }
}
