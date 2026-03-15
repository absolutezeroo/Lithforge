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
        public readonly string EntityTypeId;
        public readonly Func<ContainerScreen> Factory;
        public readonly Action<ContainerScreen, BlockEntityBase> OpenAction;
        public ContainerScreen Screen;

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
