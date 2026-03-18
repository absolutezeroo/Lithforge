using Lithforge.Voxel.BlockEntity;
using Lithforge.Item.Crafting;
using Lithforge.Item;

namespace Lithforge.Runtime.BlockEntity.Factories
{
    /// <summary>
    /// Factory for creating FurnaceBlockEntity instances.
    /// Holds references to SmeltingRecipeRegistry and ItemRegistry needed
    /// for furnace behavior construction.
    /// </summary>
    public sealed class FurnaceBlockEntityFactory : IBlockEntityFactory
    {
        private readonly SmeltingRecipeRegistry _recipeRegistry;
        private readonly ItemRegistry _itemRegistry;

        public FurnaceBlockEntityFactory(SmeltingRecipeRegistry recipeRegistry, ItemRegistry itemRegistry)
        {
            _recipeRegistry = recipeRegistry;
            _itemRegistry = itemRegistry;
        }

        public IBlockEntity Create()
        {
            return new FurnaceBlockEntity(_recipeRegistry, _itemRegistry);
        }
    }
}
