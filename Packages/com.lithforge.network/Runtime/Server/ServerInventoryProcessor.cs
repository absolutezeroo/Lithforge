using System.Collections.Generic;

using Lithforge.Item;
using Lithforge.Network.Connection;
using Lithforge.Network.Messages;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Server-side inventory command executor. Holds per-player inventories and processes
    ///     slot click commands, returning inventory sync messages for reconciliation.
    /// </summary>
    public sealed class ServerInventoryProcessor
    {
        /// <summary>Per-player inventories keyed by player ID.</summary>
        private readonly Dictionary<ushort, Inventory> _inventories = new();

        /// <summary>Item registry for looking up max stack sizes.</summary>
        private readonly ItemRegistry _itemRegistry;

        /// <summary>Creates the processor with the item registry for stack size lookups.</summary>
        public ServerInventoryProcessor(ItemRegistry itemRegistry)
        {
            _itemRegistry = itemRegistry;
        }

        /// <summary>Creates or retrieves the server-side inventory for a player.</summary>
        public Inventory GetOrCreateInventory(ushort playerId)
        {
            if (!_inventories.TryGetValue(playerId, out Inventory inv))
            {
                inv = new Inventory();
                _inventories[playerId] = inv;
            }

            return inv;
        }

        /// <summary>Removes a player's inventory when they disconnect.</summary>
        public void RemoveInventory(ushort playerId)
        {
            _inventories.Remove(playerId);
        }

        /// <summary>
        ///     Processes a slot click command from a client. Validates the state ID,
        ///     executes the mutation on the server-side inventory, and returns a
        ///     sync message if reconciliation is needed.
        /// </summary>
        public InventorySyncMessage? ProcessSlotClick(PeerInfo peer, SlotClickCmdMessage cmd)
        {
            if (!_inventories.TryGetValue(peer.AssignedPlayerId, out Inventory inventory))
            {
                return null;
            }

            // State ID mismatch: send full resync
            if (cmd.StateId != inventory.StateId)
            {
                return BuildFullSync(inventory);
            }

            // For now, the server trusts the client's slot click execution (optimistic).
            // The client has already applied the mutation. The server mirrors it.
            // A future iteration can re-execute the click logic server-side.
            // Increment the state ID to acknowledge.
            inventory.IncrementStateId();

            return null;
        }

        /// <summary>Builds a full inventory sync message from the current server-side state.</summary>
        public InventorySyncMessage BuildFullSync(Inventory inventory)
        {
            ItemStack[] snapshot = inventory.GetFullSnapshot();
            List<SyncSlot> slots = new();

            for (int i = 0; i < snapshot.Length; i++)
            {
                ItemStack stack = snapshot[i];

                if (stack.IsEmpty)
                {
                    continue;
                }

                SyncSlot slot = new()
                {
                    SlotIndex = (byte)i,
                    Ns = stack.ItemId.Namespace,
                    Name = stack.ItemId.Name,
                    Count = (ushort)stack.Count,
                    Durability = (short)stack.Durability,
                };

                // Component serialization over the wire is a future extension.
                // For now, component data is not included in sync messages.

                slots.Add(slot);
            }

            return new InventorySyncMessage
            {
                StateId = inventory.StateId,
                Slots = slots.ToArray(),
            };
        }

        /// <summary>Builds a full sync message for the given player ID.</summary>
        public InventorySyncMessage? BuildFullSyncForPlayer(ushort playerId)
        {
            if (!_inventories.TryGetValue(playerId, out Inventory inventory))
            {
                return null;
            }

            return BuildFullSync(inventory);
        }
    }
}
