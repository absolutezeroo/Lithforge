using System.IO;

namespace Lithforge.Voxel.BlockEntity
{
    /// <summary>
    /// Core interface for block entities — blocks with per-instance state
    /// (inventories, processing progress, etc.). Implemented in Tier 3.
    /// </summary>
    public interface IBlockEntity
    {
        /// <summary>
        /// The type identifier for this block entity (e.g., "lithforge:chest").
        /// Must match the TypeId registered in BlockEntityRegistry.
        /// </summary>
        public string TypeId { get; }

        /// <summary>
        /// Serializes entity-specific state to a binary stream.
        /// Called during chunk save.
        /// </summary>
        public void Serialize(BinaryWriter writer);

        /// <summary>
        /// Deserializes entity-specific state from a binary stream.
        /// Called during chunk load.
        /// </summary>
        public void Deserialize(BinaryReader reader);

        /// <summary>
        /// Called when the owning chunk is unloaded.
        /// Use for cleanup of non-persistent state.
        /// </summary>
        public void OnChunkUnload();

        /// <summary>
        /// Injects the host callback so the entity can notify its chunk of state changes.
        /// Called immediately after the entity is placed or loaded into a chunk.
        /// </summary>
        public void SetHost(IBlockEntityHost host);
    }
}
