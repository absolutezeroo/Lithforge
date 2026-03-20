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
        /// <summary>Host callback for marking the owning chunk dirty on state changes.</summary>
        private IBlockEntityHost _host;

        /// <summary>Unique type identifier used for factory dispatch and serialization.</summary>
        public abstract string TypeId { get; }

        /// <summary>
        /// The behaviors composing this block entity, in tick/serialization order.
        /// Set by the concrete subclass constructor.
        /// </summary>
        protected BlockEntityBehavior[] Behaviors { get; set; }

        /// <summary>
        ///     Injects the host callback so this entity can notify its chunk of state changes.
        ///     Wires the dirty notification to each behavior via the virtual SetOnChanged method.
        /// </summary>
        public void SetHost(IBlockEntityHost host)
        {
            _host = host;

            if (Behaviors is null)
            {
                return;
            }

            for (int i = 0; i < Behaviors.Length; i++)
            {
                Behaviors[i].SetOnChanged(NotifyDirty);
            }
        }

        /// <summary>
        ///     Marks the owning chunk dirty. Called by behavior callbacks when persistent state changes.
        ///     No-op if no host has been injected (e.g. during deserialization before placement).
        /// </summary>
        protected void NotifyDirty()
        {
            _host?.NotifyDirty();
        }

        /// <summary>Serializes all behaviors to the writer in order.</summary>
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

        /// <summary>Deserializes all behaviors from the reader in order.</summary>
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

        /// <summary>Notifies all behaviors that the owning chunk is being unloaded.</summary>
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
