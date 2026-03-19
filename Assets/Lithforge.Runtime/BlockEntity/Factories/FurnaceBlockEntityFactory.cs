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
        /// <summary>Item registry for resolving result item max stack sizes during smelting.</summary>
        private readonly ItemRegistry _itemRegistry;

        /// <summary>Smelting recipe registry for matching inputs to outputs.</summary>
        private readonly SmeltingRecipeRegistry _recipeRegistry;

        /// <summary>Creates a furnace factory with the required recipe and item registries.</summary>
        public FurnaceBlockEntityFactory(SmeltingRecipeRegistry recipeRegistry, ItemRegistry itemRegistry)
        {
            _recipeRegistry = recipeRegistry;
            _itemRegistry = itemRegistry;
        }

        /// <summary>Creates a new FurnaceBlockEntity with smelting and fuel burn behaviors.</summary>
        public IBlockEntity Create()
        {
            return new FurnaceBlockEntity(_recipeRegistry, _itemRegistry);
        }
    }
}
