using System.IO;

namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    /// Abstract base class for composable block entity behaviors.
    /// Each behavior handles one aspect of block entity logic
    /// (inventory, fuel, smelting, etc.).
    /// </summary>
    public abstract class BlockEntityBehavior
    {
        /// <summary>
        /// Called each tick cycle. deltaTime includes the bucket multiplier
        /// (e.g., 20x for 20-bucket round-robin).
        /// </summary>
        public virtual void Tick(float deltaTime)
        {
        }

        /// <summary>
        /// Serializes this behavior's state to the binary stream.
        /// </summary>
        public virtual void Serialize(BinaryWriter writer)
        {
        }

        /// <summary>
        /// Deserializes this behavior's state from the binary stream.
        /// </summary>
        public virtual void Deserialize(BinaryReader reader)
        {
        }

        /// <summary>
        /// Called when the owning chunk is unloaded.
        /// </summary>
        public virtual void OnChunkUnload()
        {
        }
    }
}
