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
        /// <summary>Item registry for looking up item definitions and tags during assembly.</summary>
        private readonly ItemRegistry _itemRegistry;

        /// <summary>Material registry for resolving tool material stats during assembly.</summary>
        private readonly ToolMaterialRegistry _materialRegistry;

        /// <summary>Creates a tool station factory with the required material and item registries.</summary>
        public ToolStationBlockEntityFactory(
            ToolMaterialRegistry materialRegistry,
            ItemRegistry itemRegistry)
        {
            _materialRegistry = materialRegistry;
            _itemRegistry = itemRegistry;
        }

        /// <summary>Creates a new ToolStationBlockEntity with inventory and assembly behaviors.</summary>
        public IBlockEntity Create()
        {
            return new ToolStationBlockEntity(_materialRegistry, _itemRegistry);
        }
    }
}
