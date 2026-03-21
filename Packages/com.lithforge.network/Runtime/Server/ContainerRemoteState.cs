using Lithforge.Item;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Snapshot of container slot state last confirmed sent to a specific client.
    ///     Dynamic-size variant of <see cref="InventoryRemoteState" /> for block entity
    ///     containers whose slot count varies (27 for chest, 3 for furnace, etc.).
    ///     Compared against the live <see cref="IItemStorage" /> each Phase 5 tick
    ///     to detect dirty slots.
    /// </summary>
    public sealed class ContainerRemoteState
    {
        /// <summary>Pre-allocated snapshot array sized to match the container's slot count.</summary>
        private readonly ItemStack[] _remoteSlots;

        /// <summary>Creates a new remote state snapshot for a container with the given slot count.</summary>
        public ContainerRemoteState(int slotCount)
        {
            _remoteSlots = new ItemStack[slotCount];
        }

        /// <summary>Number of tracked slots.</summary>
        public int SlotCount
        {
            get { return _remoteSlots.Length; }
        }

        /// <summary>Returns true if the given slot differs from the remote snapshot.</summary>
        public bool IsSlotDirty(int index, ItemStack current)
        {
            if (index < 0 || index >= _remoteSlots.Length)
            {
                return false;
            }

            return _remoteSlots[index] != current;
        }

        /// <summary>Updates the snapshot for a single slot after sending it to the client.</summary>
        public void MarkSlotSent(int index, ItemStack value)
        {
            if (index >= 0 && index < _remoteSlots.Length)
            {
                _remoteSlots[index] = value;
            }
        }

        /// <summary>Updates all slots from the current container state (used after initial sync).</summary>
        public void SyncAll(IItemStorage storage)
        {
            int count = storage.SlotCount < _remoteSlots.Length
                ? storage.SlotCount
                : _remoteSlots.Length;

            for (int i = 0; i < count; i++)
            {
                _remoteSlots[i] = storage.GetSlot(i);
            }
        }
    }
}
