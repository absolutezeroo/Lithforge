using Lithforge.Voxel.Crafting;
using Lithforge.Voxel.Item;

namespace Lithforge.Runtime.UI.Container
{
    /// <summary>
    /// Adapts a CraftingGrid to the ISlotContainer interface.
    /// Linearizes the 2D grid (row-major: index = y * width + x).
    /// Calls CraftingEngine.FindMatch on slot changes to update the output.
    /// </summary>
    public sealed class CraftingGridContainerAdapter : ISlotContainer
    {
        private readonly CraftingGrid _grid;
        private readonly CraftingEngine _engine;
        private readonly CraftingOutputContainerAdapter _output;

        public CraftingGridContainerAdapter(
            CraftingGrid grid,
            CraftingEngine engine,
            CraftingOutputContainerAdapter output)
        {
            _grid = grid;
            _engine = engine;
            _output = output;
        }

        public int SlotCount
        {
            get { return _grid.Width * _grid.Height; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public ItemStack GetSlot(int index)
        {
            int x = index % _grid.Width;
            int y = index / _grid.Width;
            return _grid.GetSlotStack(x, y);
        }

        public void SetSlot(int index, ItemStack stack)
        {
            int x = index % _grid.Width;
            int y = index / _grid.Width;
            _grid.SetSlotStack(x, y, stack);
        }

        public void OnSlotChanged(int index)
        {
            // Recheck recipe match whenever a craft slot changes
            RecipeEntry match = _engine.FindMatch(_grid);
            _output.SetRecipeMatch(match);
        }

        public CraftingGrid Grid
        {
            get { return _grid; }
        }
    }
}
