using System.IO;
using Lithforge.Voxel.BlockEntity;

namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    /// Abstract base class for block entities in Tier 3.
    /// Composes an ordered array of BlockEntityBehavior instances.
    /// Delegates Serialize, Deserialize, Tick, and OnChunkUnload to behaviors in order.
    /// </summary>
    public abstract class BlockEntity : IBlockEntity
    {
        public abstract string TypeId { get; }

        /// <summary>
        /// The behaviors composing this block entity, in tick/serialization order.
        /// Set by the concrete subclass constructor.
        /// </summary>
        protected BlockEntityBehavior[] Behaviors { get; set; }

        public virtual void Serialize(BinaryWriter writer)
        {
            if (Behaviors == null)
            {
                return;
            }

            for (int i = 0; i < Behaviors.Length; i++)
            {
                Behaviors[i].Serialize(writer);
            }
        }

        public virtual void Deserialize(BinaryReader reader)
        {
            if (Behaviors == null)
            {
                return;
            }

            for (int i = 0; i < Behaviors.Length; i++)
            {
                Behaviors[i].Deserialize(reader);
            }
        }

        /// <summary>
        /// Ticks all behaviors in order. Called by BlockEntityTickScheduler.
        /// </summary>
        public virtual void Tick(float deltaTime)
        {
            if (Behaviors == null)
            {
                return;
            }

            for (int i = 0; i < Behaviors.Length; i++)
            {
                Behaviors[i].Tick(deltaTime);
            }
        }

        public virtual void OnChunkUnload()
        {
            if (Behaviors == null)
            {
                return;
            }

            for (int i = 0; i < Behaviors.Length; i++)
            {
                Behaviors[i].OnChunkUnload();
            }
        }

        /// <summary>
        /// Finds the first behavior of type T.
        /// Returns null if not found.
        /// </summary>
        public T GetBehavior<T>() where T : BlockEntityBehavior
        {
            if (Behaviors == null)
            {
                return null;
            }

            for (int i = 0; i < Behaviors.Length; i++)
            {
                if (Behaviors[i] is T typed)
                {
                    return typed;
                }
            }

            return null;
        }
    }
}
