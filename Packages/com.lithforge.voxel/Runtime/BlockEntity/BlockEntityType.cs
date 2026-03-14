namespace Lithforge.Voxel.BlockEntity
{
    /// <summary>
    /// Pairs a type identifier string with a factory for creating block entities
    /// of that type. Registered in BlockEntityRegistry.
    /// </summary>
    public sealed class BlockEntityType
    {
        public string TypeId { get; }

        public IBlockEntityFactory Factory { get; }

        public BlockEntityType(string typeId, IBlockEntityFactory factory)
        {
            TypeId = typeId;
            Factory = factory;
        }
    }
}
