using System;
using BlockEntityBase = Lithforge.Runtime.BlockEntity.BlockEntity;

namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    /// Internal data class pairing a block entity type ID to its screen factory
    /// and typed open action. Owned by <see cref="ContainerScreenManager"/>.
    /// The screen instance is lazily created on first use.
    /// </summary>
    internal sealed class BlockEntityScreenBinding
    {
        /// <summary>The block entity type ID this binding dispatches for.</summary>
        public readonly string EntityTypeId;

        /// <summary>Factory delegate that lazily creates the screen instance on first use.</summary>
        public readonly Func<ContainerScreen> Factory;

        /// <summary>Action that casts the entity and calls the screen's typed OpenForEntity method.</summary>
        public readonly Action<ContainerScreen, BlockEntityBase> OpenAction;

        /// <summary>Cached screen instance, null until first use.</summary>
        public ContainerScreen Screen;

        /// <summary>Creates a binding for the given entity type with a factory and open action.</summary>
        public BlockEntityScreenBinding(
            string entityTypeId,
            Func<ContainerScreen> factory,
            Action<ContainerScreen, BlockEntityBase> openAction)
        {
            EntityTypeId = entityTypeId;
            Factory = factory;
            OpenAction = openAction;
            Screen = null;
        }
    }
}
