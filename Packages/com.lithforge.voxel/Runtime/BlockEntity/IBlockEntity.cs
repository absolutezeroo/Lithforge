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
        string TypeId { get; }

        /// <summary>
        /// Serializes entity-specific state to a binary stream.
        /// Called during chunk save.
        /// </summary>
        void Serialize(BinaryWriter writer);

        /// <summary>
        /// Deserializes entity-specific state from a binary stream.
        /// Called during chunk load.
        /// </summary>
        void Deserialize(BinaryReader reader);

        /// <summary>
        /// Called when the owning chunk is unloaded.
        /// Use for cleanup of non-persistent state.
        /// </summary>
        void OnChunkUnload();
    }
}
