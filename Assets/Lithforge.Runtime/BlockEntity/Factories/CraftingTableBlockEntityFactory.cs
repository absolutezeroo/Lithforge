using Lithforge.Voxel.BlockEntity;

namespace Lithforge.Runtime.BlockEntity.Factories
{
    /// <summary>
    /// Factory for creating CraftingTableBlockEntity instances.
    /// </summary>
    public sealed class CraftingTableBlockEntityFactory : IBlockEntityFactory
    {
        public IBlockEntity Create()
        {
            return new CraftingTableBlockEntity();
        }
    }
}
