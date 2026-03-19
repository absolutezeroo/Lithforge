namespace Lithforge.Voxel.BlockEntity
{
    /// <summary>
    /// Pairs a type identifier string with a factory for creating block entities
    /// of that type. Registered in BlockEntityRegistry.
    /// </summary>
    public sealed class BlockEntityType
    {
        /// <summary>Unique string identifier for this block entity type (e.g. "chest", "furnace").</summary>
        public string TypeId { get; }

        /// <summary>Factory responsible for creating instances of this block entity type.</summary>
        public IBlockEntityFactory Factory { get; }

        /// <summary>Creates a block entity type registration with the given ID and factory.</summary>
        public BlockEntityType(string typeId, IBlockEntityFactory factory)
        {
            TypeId = typeId;
            Factory = factory;
        }
    }
}
