namespace Lithforge.Voxel.BlockEntity
{
    /// <summary>
    /// Factory interface for creating block entity instances.
    /// Each concrete factory knows how to construct its entity type
    /// with the correct behaviors and dependencies.
    /// </summary>
    public interface IBlockEntityFactory
    {
        /// <summary>
        /// Creates a new, empty block entity instance.
        /// </summary>
        IBlockEntity Create();
    }
}
