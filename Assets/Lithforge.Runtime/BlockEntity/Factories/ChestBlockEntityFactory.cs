using Lithforge.Voxel.BlockEntity;

namespace Lithforge.Runtime.BlockEntity.Factories
{
    /// <summary>
    /// Factory for creating ChestBlockEntity instances.
    /// </summary>
    public sealed class ChestBlockEntityFactory : IBlockEntityFactory
    {
        /// <summary>Creates a new ChestBlockEntity with default 27-slot inventory.</summary>
        public IBlockEntity Create()
        {
            return new ChestBlockEntity();
        }
    }
}
