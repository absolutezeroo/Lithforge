using Lithforge.Voxel.BlockEntity;

namespace Lithforge.Runtime.BlockEntity.Factories
{
    /// <summary>
    /// Factory for Part Builder block entities.
    /// </summary>
    public sealed class PartBuilderBlockEntityFactory : IBlockEntityFactory
    {
        public IBlockEntity Create()
        {
            return new PartBuilderBlockEntity();
        }
    }
}
