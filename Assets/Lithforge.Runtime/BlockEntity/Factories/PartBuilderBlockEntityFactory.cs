using Lithforge.Voxel.BlockEntity;

namespace Lithforge.Runtime.BlockEntity.Factories
{
    /// <summary>
    /// Factory for Part Builder block entities.
    /// </summary>
    public sealed class PartBuilderBlockEntityFactory : IBlockEntityFactory
    {
        /// <summary>Creates a new PartBuilderBlockEntity with material, pattern, and output slots.</summary>
        public IBlockEntity Create()
        {
            return new PartBuilderBlockEntity();
        }
    }
}
