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
        /// <summary>The crafting engine used for recipe matching.</summary>
        private readonly CraftingEngine _engine;

        /// <summary>The output adapter that receives recipe match results.</summary>
        private readonly CraftingOutputContainerAdapter _output;

        /// <summary>Creates an adapter wrapping a crafting grid with recipe matching via the engine.</summary>
        public CraftingGridContainerAdapter(
            CraftingGrid grid,
            CraftingEngine engine,
            CraftingOutputContainerAdapter output)
        {
            Grid = grid;
            _engine = engine;
            _output = output;
        }

        /// <summary>Total number of craft grid slots (width times height).</summary>
        public int SlotCount
        {
            get { return Grid.Width * Grid.Height; }
        }

        /// <summary>Returns false because crafting grid slots accept item placement.</summary>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>Gets the item stack at the given linearized grid index (row-major).</summary>
        public ItemStack GetSlot(int index)
        {
            int x = index % Grid.Width;
            int y = index / Grid.Width;
            return Grid.GetSlotStack(x, y);
        }

        /// <summary>Sets the item stack at the given linearized grid index (row-major).</summary>
        public void SetSlot(int index, ItemStack stack)
        {
            int x = index % Grid.Width;
            int y = index / Grid.Width;
            Grid.SetSlotStack(x, y, stack);
        }

        /// <summary>Rechecks the recipe match whenever a craft slot changes and updates the output.</summary>
        public void OnSlotChanged(int index)
        {
            // Recheck recipe match whenever a craft slot changes
            RecipeEntry match = _engine.FindMatch(Grid);
            _output.SetRecipeMatch(match);
        }

        /// <summary>The underlying crafting grid being adapted.</summary>
        public CraftingGrid Grid { get; }
    }
}
