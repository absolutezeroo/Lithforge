using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Item;
using Lithforge.Item.Interaction;
using Lithforge.Network.Connection;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using Lithforge.Voxel.Storage;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Server-authoritative inventory processor. Holds per-player inventories,
    ///     re-executes slot click commands via <see cref="SlotActionExecutor" />,
    ///     reconciles against client predictions, and broadcasts per-tick deltas.
    /// </summary>
    public sealed class ServerInventoryProcessor
    {
        /// <summary>Per-player inventories keyed by player ID.</summary>
        private readonly Dictionary<ushort, Inventory> _inventories = new();

        /// <summary>Item registry for looking up max stack sizes.</summary>
        private readonly ItemRegistry _itemRegistry;

        /// <summary>Network server for sending correction messages.</summary>
        private readonly INetworkServer _server;

        /// <summary>Concrete server for peer lookup.</summary>
        private readonly NetworkServer _serverImpl;

        /// <summary>Creates the processor with all required dependencies.</summary>
        public ServerInventoryProcessor(
            ItemRegistry itemRegistry,
            INetworkServer server,
            NetworkServer serverImpl)
        {
            _itemRegistry = itemRegistry;
            _server = server;
            _serverImpl = serverImpl;
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
        ///     Restores a player's server-side inventory from saved player data.
        ///     Called during player accept before <see cref="InitializePlayerState" />.
        /// </summary>
        public void RestoreInventoryFromSave(ushort playerId, WorldPlayerState playerData)
        {
            if (playerData?.Slots is null)
            {
                return;
            }

            if (!_inventories.TryGetValue(playerId, out Inventory inventory))
            {
                return;
            }

            for (int i = 0; i < playerData.Slots.Length; i++)
            {
                SavedItemStack saved = playerData.Slots[i];

                if (saved.Count <= 0 || saved.Slot < 0 || saved.Slot >= Inventory.SlotCount)
                {
                    continue;
                }

                ResourceId itemId = new(saved.Ns, saved.Name);
                ItemStack stack = new(itemId, saved.Count, saved.Durability);
                inventory.SetSlot(saved.Slot, stack);
            }

            inventory.SelectedSlot = playerData.SelectedSlot;
        }

        /// <summary>
        ///     Initializes the per-player remote state and sends the initial full sync.
        ///     Called during player accept after the inventory has been populated.
        /// </summary>
        public void InitializePlayerState(PeerInfo peer)
        {
            ushort playerId = peer.AssignedPlayerId;

            if (!_inventories.TryGetValue(playerId, out Inventory inventory))
            {
                return;
            }

            if (peer.InterestState is null)
            {
                return;
            }

            InventoryRemoteState remote = new();
            peer.InterestState.InventoryRemote = remote;
            peer.InterestState.ServerCursor = ItemStack.Empty;

            // Send initial full sync
            InventorySyncMessage sync = BuildFullSync(inventory, ItemStack.Empty);
            _server.SendTo(peer.ConnectionId, sync, PipelineId.ReliableSequenced);

            // Mark all slots as sent
            remote.SyncAll(inventory, ItemStack.Empty);
        }

        /// <summary>
        ///     Processes a slot click command from a client. Validates the state ID,
        ///     re-executes the mutation via SlotActionExecutor, compares against
        ///     client predictions, and sends corrections if needed.
        /// </summary>
        public InventorySyncMessage? ProcessSlotClick(PeerInfo peer, SlotClickCmdMessage cmd)
        {
            if (!_inventories.TryGetValue(peer.AssignedPlayerId, out Inventory inventory))
            {
                return null;
            }

            ItemStack cursor = peer.InterestState?.ServerCursor ?? ItemStack.Empty;

            // State ID mismatch: send full resync, do not execute
            if (cmd.StateId != inventory.StateId)
            {
                InventorySyncMessage resync = BuildFullSync(inventory, cursor);

                if (peer.InterestState?.InventoryRemote is not null)
                {
                    peer.InterestState.InventoryRemote.SyncAll(inventory, cursor);
                }

                return resync;
            }

            // Crafting output take (ClickType=5): trusted pass-through for core scope.
            // Apply the client's predicted slot changes directly.
            if (cmd.ClickType == SlotActionExecutor.ClickOutputTake)
            {
                ApplyTrustedPredictions(inventory, ref cursor, cmd);

                if (peer.InterestState is not null)
                {
                    peer.InterestState.ServerCursor = cursor;
                }

                return null;
            }

            // Paint-drag: 3-phase protocol (Begin=0, Slot=1, End=2)
            if (cmd.ClickType == SlotActionExecutor.ClickPaintDrag)
            {
                ProcessPaintDrag(peer, inventory, ref cursor, cmd);

                if (peer.InterestState is not null)
                {
                    peer.InterestState.ServerCursor = cursor;
                }

                return null;
            }

            // Re-execute the click on the server's authoritative state
            SlotActionResult result = SlotActionExecutor.Execute(
                inventory, ref cursor, cmd.SlotIndex, cmd.ClickType, cmd.Button, _itemRegistry);

            if (result.Outcome == SlotActionOutcome.ItemCountMismatch)
            {
                // Anti-dupe violation: send full resync
                InventorySyncMessage resync = BuildFullSync(inventory, cursor);

                if (peer.InterestState?.InventoryRemote is not null)
                {
                    peer.InterestState.InventoryRemote.SyncAll(inventory, cursor);
                }

                return resync;
            }

            if (peer.InterestState is not null)
            {
                peer.InterestState.ServerCursor = cursor;
            }

            if (result.Outcome is SlotActionOutcome.InvalidSlot or SlotActionOutcome.InvalidAction)
            {
                return null;
            }

            // Compare server result against client predictions
            if (!PredictionsMatch(inventory, cursor, cmd))
            {
                SendSlotCorrections(peer, inventory, cursor, cmd);
            }

            // Update remote state for all affected slots to prevent redundant delta sync.
            // When predictions matched, this prevents BroadcastInventoryDeltas from re-sending.
            // When predictions mismatched, SendSlotCorrections already sent the corrections.
            SyncRemoteStateForResult(peer, inventory, cursor, result);

            return null;
        }

        /// <summary>
        ///     Per-tick delta sync. Compares each playing peer's current inventory
        ///     against what was last sent, sending InventorySlotUpdateMessages for changes.
        ///     Called from Phase 5 broadcast.
        /// </summary>
        public void BroadcastInventoryDeltas(List<PeerInfo> playingPeers)
        {
            for (int i = 0; i < playingPeers.Count; i++)
            {
                PeerInfo peer = playingPeers[i];
                InventoryRemoteState remote = peer.InterestState?.InventoryRemote;

                if (remote is null)
                {
                    continue;
                }

                if (!_inventories.TryGetValue(peer.AssignedPlayerId, out Inventory inventory))
                {
                    continue;
                }

                uint stateId = inventory.StateId;
                ItemStack cursor = peer.InterestState.ServerCursor;

                // Check each slot for changes
                for (int slot = 0; slot < Inventory.SlotCount; slot++)
                {
                    ItemStack current = inventory.GetSlot(slot);

                    if (!remote.IsSlotDirty(slot, current))
                    {
                        continue;
                    }

                    InventorySlotUpdateMessage update = BuildSlotUpdate(
                        stateId, (short)slot, current);

                    _server.SendTo(peer.ConnectionId, update, PipelineId.ReliableSequenced);
                    remote.MarkSlotSent(slot, current);
                }

                // Check cursor for changes
                if (remote.IsCursorDirty(cursor))
                {
                    InventorySlotUpdateMessage cursorUpdate = BuildSlotUpdate(
                        stateId, InventorySlotUpdateMessage.CursorSlotIndex, cursor);

                    _server.SendTo(peer.ConnectionId, cursorUpdate, PipelineId.ReliableSequenced);
                    remote.MarkCursorSent(cursor);
                }
            }
        }

        /// <summary>
        ///     Returns the cursor to the player's inventory. Called on container close
        ///     or player disconnect.
        /// </summary>
        public void ReturnCursorToInventory(ushort playerId)
        {
            if (!_inventories.TryGetValue(playerId, out Inventory inventory))
            {
                return;
            }

            PeerInfo peer = FindPeerByPlayerId(playerId);
            ItemStack cursor = peer?.InterestState?.ServerCursor ?? ItemStack.Empty;

            if (cursor.IsEmpty)
            {
                return;
            }

            ItemEntry def = _itemRegistry.Get(cursor.ItemId);
            int maxStack = def?.MaxStackSize ?? 64;

            if (cursor.HasComponents)
            {
                inventory.AddItemStack(cursor);
            }
            else
            {
                inventory.AddItem(cursor.ItemId, cursor.Count, maxStack);
            }

            if (peer?.InterestState is not null)
            {
                peer.InterestState.ServerCursor = ItemStack.Empty;
            }
        }

        /// <summary>Builds a full inventory sync message from the current server-side state.</summary>
        public InventorySyncMessage BuildFullSync(Inventory inventory, ItemStack cursor)
        {
            ItemStack[] snapshot = inventory.GetFullSnapshot();

            // Pre-count non-empty slots to avoid List allocation
            int nonEmpty = 0;

            for (int i = 0; i < snapshot.Length; i++)
            {
                if (!snapshot[i].IsEmpty)
                {
                    nonEmpty++;
                }
            }

            SyncSlot[] slots = new SyncSlot[nonEmpty];
            int writeIndex = 0;

            for (int i = 0; i < snapshot.Length; i++)
            {
                ItemStack stack = snapshot[i];

                if (stack.IsEmpty)
                {
                    continue;
                }

                slots[writeIndex++] = new SyncSlot
                {
                    SlotIndex = (byte)i,
                    Ns = stack.ItemId.Namespace,
                    Name = stack.ItemId.Name,
                    Count = (ushort)stack.Count,
                    Durability = (short)stack.Durability,
                };
            }

            InventorySyncMessage msg = new()
            {
                StateId = inventory.StateId,
                Slots = slots,
                HasCursor = !cursor.IsEmpty,
            };

            if (!cursor.IsEmpty)
            {
                msg.CursorNs = cursor.ItemId.Namespace;
                msg.CursorName = cursor.ItemId.Name;
                msg.CursorCount = (ushort)cursor.Count;
                msg.CursorDurability = (short)cursor.Durability;
            }

            return msg;
        }

        /// <summary>Builds a full sync message for the given player ID.</summary>
        public InventorySyncMessage? BuildFullSyncForPlayer(ushort playerId)
        {
            if (!_inventories.TryGetValue(playerId, out Inventory inventory))
            {
                return null;
            }

            PeerInfo peer = FindPeerByPlayerId(playerId);
            ItemStack cursor = peer?.InterestState?.ServerCursor ?? ItemStack.Empty;

            return BuildFullSync(inventory, cursor);
        }

        /// <summary>
        ///     Serializes a player's server-side inventory into a <see cref="WorldPlayerState" />
        ///     for persistence. Populates the Slots and SelectedSlot fields.
        /// </summary>
        public void SerializeInventoryInto(ushort playerId, WorldPlayerState state)
        {
            if (!_inventories.TryGetValue(playerId, out Inventory inventory))
            {
                return;
            }

            state.SelectedSlot = inventory.SelectedSlot;
            ItemStack[] snapshot = inventory.GetFullSnapshot();
            List<SavedItemStack> saved = new();

            for (int i = 0; i < snapshot.Length; i++)
            {
                ItemStack stack = snapshot[i];

                if (stack.IsEmpty)
                {
                    continue;
                }

                saved.Add(new SavedItemStack(
                    i,
                    stack.ItemId.Namespace,
                    stack.ItemId.Name,
                    stack.Count,
                    stack.Durability));
            }

            state.Slots = saved.ToArray();
        }

        /// <summary>Processes a paint-drag sub-message (Begin/Slot/End).</summary>
        private void ProcessPaintDrag(
            PeerInfo peer,
            Inventory inventory,
            ref ItemStack cursor,
            SlotClickCmdMessage cmd)
        {
            PaintDragState drag = peer.InterestState?.PaintDrag;

            switch (cmd.Button)
            {
                case 0: // Begin
                    drag ??= new PaintDragState();

                    if (peer.InterestState is not null)
                    {
                        peer.InterestState.PaintDrag = drag;
                    }

                    drag.Reset();
                    drag.IsActive = true;
                    break;

                case 1: // Slot
                    if (drag is not { IsActive: true })
                    {
                        return;
                    }

                    if (cmd.SlotIndex < 0 || cmd.SlotIndex >= Inventory.SlotCount)
                    {
                        return;
                    }

                    if (drag.PaintedSlots.Contains(cmd.SlotIndex))
                    {
                        return;
                    }

                    // Execute single paint-slot placement
                    SlotActionResult paintResult = SlotActionExecutor.ExecutePaintSlot(
                        inventory, ref cursor, cmd.SlotIndex, _itemRegistry);

                    if (paintResult.Outcome == SlotActionOutcome.Success)
                    {
                        drag.PaintedSlots.Add(cmd.SlotIndex);
                    }

                    break;

                case 2: // End
                    drag?.Reset();
                    break;
            }
        }

        /// <summary>
        ///     Applies client-predicted slot changes directly to the server inventory.
        ///     Used for crafting output take (trusted) where the server cannot re-execute.
        /// </summary>
        private void ApplyTrustedPredictions(
            Inventory inventory,
            ref ItemStack cursor,
            SlotClickCmdMessage cmd)
        {
            if (cmd.PredictedSlots is not null)
            {
                for (int i = 0; i < cmd.PredictedSlots.Length; i++)
                {
                    PredictedSlot pred = cmd.PredictedSlots[i];

                    if (pred.SlotIndex >= Inventory.SlotCount)
                    {
                        continue;
                    }

                    if (pred.Count == 0)
                    {
                        inventory.SetSlot(pred.SlotIndex, ItemStack.Empty);
                    }
                    else
                    {
                        ResourceId itemId = new(pred.Ns ?? "", pred.Name ?? "");
                        ItemStack stack = new(itemId, pred.Count, pred.Durability);
                        inventory.SetSlot(pred.SlotIndex, stack);
                    }
                }
            }

            // Apply predicted cursor
            if (cmd.CursorCount > 0)
            {
                ResourceId cursorId = new(cmd.CursorNs ?? "", cmd.CursorName ?? "");
                cursor = new ItemStack(cursorId, cmd.CursorCount, cmd.CursorDurability);
            }
            else
            {
                cursor = ItemStack.Empty;
            }
        }

        /// <summary>Checks whether the client's predicted slot changes match the server's state.</summary>
        private bool PredictionsMatch(
            Inventory inventory,
            ItemStack cursor,
            SlotClickCmdMessage cmd)
        {
            // Check predicted slots
            if (cmd.PredictedSlots is not null)
            {
                for (int i = 0; i < cmd.PredictedSlots.Length; i++)
                {
                    PredictedSlot pred = cmd.PredictedSlots[i];

                    if (pred.SlotIndex >= Inventory.SlotCount)
                    {
                        continue;
                    }

                    ItemStack actual = inventory.GetSlot(pred.SlotIndex);

                    if (!SlotMatchesPrediction(actual, pred))
                    {
                        return false;
                    }
                }
            }

            // Check predicted cursor
            if (cmd.CursorCount == 0)
            {
                if (!cursor.IsEmpty)
                {
                    return false;
                }
            }
            else
            {
                if (cursor.IsEmpty)
                {
                    return false;
                }

                ResourceId predId = new(cmd.CursorNs ?? "", cmd.CursorName ?? "");

                if (cursor.ItemId != predId || cursor.Count != cmd.CursorCount
                                             || cursor.Durability != cmd.CursorDurability)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Sends targeted corrections for slots that differ from predictions.</summary>
        private void SendSlotCorrections(
            PeerInfo peer,
            Inventory inventory,
            ItemStack cursor,
            SlotClickCmdMessage cmd)
        {
            uint stateId = inventory.StateId;

            // Send corrections for mismatched predicted slots
            if (cmd.PredictedSlots is not null)
            {
                for (int i = 0; i < cmd.PredictedSlots.Length; i++)
                {
                    PredictedSlot pred = cmd.PredictedSlots[i];

                    if (pred.SlotIndex >= Inventory.SlotCount)
                    {
                        continue;
                    }

                    ItemStack actual = inventory.GetSlot(pred.SlotIndex);

                    if (SlotMatchesPrediction(actual, pred))
                    {
                        continue;
                    }

                    InventorySlotUpdateMessage update = BuildSlotUpdate(
                        stateId, pred.SlotIndex, actual);

                    _server.SendTo(peer.ConnectionId, update, PipelineId.ReliableSequenced);

                    peer.InterestState?.InventoryRemote?.MarkSlotSent(pred.SlotIndex, actual);
                }
            }

            // Send cursor correction if needed
            bool cursorMatches;

            if (cmd.CursorCount == 0)
            {
                cursorMatches = cursor.IsEmpty;
            }
            else
            {
                ResourceId predId = new(cmd.CursorNs ?? "", cmd.CursorName ?? "");
                cursorMatches = !cursor.IsEmpty
                                && cursor.ItemId == predId
                                && cursor.Count == cmd.CursorCount
                                && cursor.Durability == cmd.CursorDurability;
            }

            if (!cursorMatches)
            {
                InventorySlotUpdateMessage cursorUpdate = BuildSlotUpdate(
                    stateId, InventorySlotUpdateMessage.CursorSlotIndex, cursor);

                _server.SendTo(peer.ConnectionId, cursorUpdate, PipelineId.ReliableSequenced);
                peer.InterestState?.InventoryRemote?.MarkCursorSent(cursor);
            }
        }

        /// <summary>Checks whether an actual ItemStack matches a predicted slot.</summary>
        private static bool SlotMatchesPrediction(ItemStack actual, PredictedSlot pred)
        {
            if (pred.Count == 0)
            {
                return actual.IsEmpty;
            }

            if (actual.IsEmpty)
            {
                return false;
            }

            ResourceId predId = new(pred.Ns ?? "", pred.Name ?? "");

            return actual.ItemId == predId
                   && actual.Count == pred.Count
                   && actual.Durability == pred.Durability;
        }

        /// <summary>
        ///     Updates the remote state for all slots and cursor affected by a click result.
        ///     Prevents BroadcastInventoryDeltas from re-sending values that the client
        ///     already has (either via correct prediction or via SendSlotCorrections).
        /// </summary>
        private static void SyncRemoteStateForResult(
            PeerInfo peer,
            Inventory inventory,
            ItemStack cursor,
            SlotActionResult result)
        {
            InventoryRemoteState remote = peer.InterestState?.InventoryRemote;

            if (remote is null)
            {
                return;
            }

            for (int i = 0; i < result.ChangedSlotCount; i++)
            {
                int slot = result.GetSlot(i);
                remote.MarkSlotSent(slot, inventory.GetSlot(slot));
            }

            if (result.CursorChanged)
            {
                remote.MarkCursorSent(cursor);
            }
        }

        /// <summary>Builds a single-slot update message for a given slot or cursor.</summary>
        private static InventorySlotUpdateMessage BuildSlotUpdate(
            uint stateId,
            short slotIndex,
            ItemStack stack)
        {
            InventorySlotUpdateMessage msg = new()
            {
                WindowId = 0,
                StateId = stateId,
                SlotIndex = slotIndex,
                Count = (ushort)stack.Count,
            };

            if (!stack.IsEmpty)
            {
                msg.Ns = stack.ItemId.Namespace;
                msg.Name = stack.ItemId.Name;
                msg.Durability = (short)stack.Durability;
            }

            return msg;
        }

        /// <summary>Finds a PeerInfo by player ID by scanning all peers.</summary>
        private PeerInfo FindPeerByPlayerId(ushort playerId)
        {
            IReadOnlyList<PeerInfo> allPeers = _serverImpl.AllPeers;

            for (int i = 0; i < allPeers.Count; i++)
            {
                if (allPeers[i].AssignedPlayerId == playerId)
                {
                    return allPeers[i];
                }
            }

            return null;
        }
    }
}
