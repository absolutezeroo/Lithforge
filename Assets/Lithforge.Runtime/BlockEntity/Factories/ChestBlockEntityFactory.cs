using Lithforge.Voxel.BlockEntity;

namespace Lithforge.Runtime.BlockEntity.Factories
{
    /// <summary>
    /// Factory for creating ChestBlockEntity instances.
    /// </summary>
    public sealed class ChestBlockEntityFactory : IBlockEntityFactory
    {
        public IBlockEntity Create()
        {
            return new ChestBlockEntity();
        }
    }
}
