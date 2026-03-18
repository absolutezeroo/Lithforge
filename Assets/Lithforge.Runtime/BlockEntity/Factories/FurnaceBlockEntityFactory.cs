using Lithforge.Voxel.BlockEntity;
using Lithforge.Item.Crafting;
using Lithforge.Item;
using Lithforge.Voxel.Crafting;

namespace Lithforge.Runtime.BlockEntity.Factories
{
    /// <summary>
    ///     Factory for creating FurnaceBlockEntity instances.
    ///     Holds references to SmeltingRecipeRegistry and ItemRegistry needed
    ///     for furnace behavior construction.
    /// </summary>
    public sealed class FurnaceBlockEntityFactory : IBlockEntityFactory
    {
        private readonly ItemRegistry _itemRegistry;
        private readonly SmeltingRecipeRegistry _recipeRegistry;

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
