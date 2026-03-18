using Lithforge.Voxel.BlockEntity;
using Lithforge.Item;

namespace Lithforge.Runtime.BlockEntity.Factories
{
    /// <summary>
    ///     Factory for creating ToolStationBlockEntity instances.
    ///     Holds references to ToolMaterialRegistry and ItemRegistry
    ///     needed for assembly behavior construction.
    /// </summary>
    public sealed class ToolStationBlockEntityFactory : IBlockEntityFactory
    {
        private readonly ItemRegistry _itemRegistry;
        private readonly ToolMaterialRegistry _materialRegistry;

        public ToolStationBlockEntityFactory(
            ToolMaterialRegistry materialRegistry,
            ItemRegistry itemRegistry)
        {
            _materialRegistry = materialRegistry;
            _itemRegistry = itemRegistry;
        }

        public IBlockEntity Create()
        {
            return new ToolStationBlockEntity(_materialRegistry, _itemRegistry);
        }
    }
}
