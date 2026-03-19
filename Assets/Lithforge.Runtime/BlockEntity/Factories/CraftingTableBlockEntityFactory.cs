using Lithforge.Voxel.BlockEntity;

namespace Lithforge.Runtime.BlockEntity.Factories
{
    /// <summary>
    /// Factory for creating CraftingTableBlockEntity instances.
    /// </summary>
    public sealed class CraftingTableBlockEntityFactory : IBlockEntityFactory
    {
        /// <summary>Creates a new CraftingTableBlockEntity with a 3x3 crafting grid.</summary>
        public IBlockEntity Create()
        {
            return new CraftingTableBlockEntity();
        }
    }
}
