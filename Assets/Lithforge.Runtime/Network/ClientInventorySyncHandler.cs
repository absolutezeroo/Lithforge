using System;

using Lithforge.Core.Data;
using Lithforge.Item;
using Lithforge.Network;
using Lithforge.Network.Client;
using Lithforge.Network.Connection;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;

namespace Lithforge.Runtime.Network
{
    /// <summary>
    ///     Client-side handler for server inventory corrections and full resyncs.
    ///     Receives <see cref="InventorySyncMessage" /> (full resync) and
    ///     <see cref="InventorySlotUpdateMessage" /> (targeted slot/cursor corrections),
    ///     applying them to the local <see cref="Inventory" />.
    ///     Also provides <see cref="SendSlotClick" /> to send slot click commands to
    ///     the server with the last known server state ID.
    /// </summary>
    public sealed class ClientInventorySyncHandler
    {
        /// <summary>The client-side player inventory to apply corrections to.</summary>
        private readonly Inventory _inventory;

        /// <summary>Network client for sending slot click commands to the server.</summary>
        private readonly INetworkClient _client;

        /// <summary>
        ///     The state ID from the last server sync or correction. Used as the
        ///     pre-click state ID when sending slot click commands. This tracks what
        ///     the server believes the client has, not the client's local prediction state.
        /// </summary>
        private uint _lastKnownServerStateId;

        /// <summary>
        ///     Fired when the server corrects the cursor (held item) state.
        ///     Container screens wire this to their <c>HeldStack.Set()</c> while open.
        /// </summary>
        public Action<ItemStack> OnCursorCorrected;

        /// <summary>Creates the sync handler with the local inventory and network client.</summary>
        public ClientInventorySyncHandler(Inventory inventory, INetworkClient client)
        {
            _inventory = inventory;
            _client = client;
            _lastKnownServerStateId = inventory.StateId;
        }

        /// <summary>Registers inbound message handlers on the client's dispatcher.</summary>
        public void RegisterHandlers(MessageDispatcher dispatcher)
        {
            dispatcher.RegisterHandler(MessageType.InventorySync, OnInventorySync);
            dispatcher.RegisterHandler(MessageType.InventorySlotUpdate, OnSlotUpdate);
        }

        /// <summary>
        ///     Sends a slot click command to the server. Called by container screens
        ///     after local prediction executes. Uses the last known server state ID
        ///     (not the local post-prediction value) so the server can match it
        ///     against its authoritative state.
        /// </summary>
        public void SendSlotClick(int slotIndex, byte clickType, byte button)
        {
            SlotClickCmdMessage msg = new()
            {
                StateId = _lastKnownServerStateId,
                SlotIndex = (short)slotIndex,
                ClickType = clickType,
                Button = button,
            };

            _client.Send(msg, PipelineId.ReliableSequenced);

            // Advance the known server state ID optimistically. The server will
            // also increment its state ID when it re-executes this click. If the
            // client is correct, both stay in sync. If not, a correction or
            // full resync will overwrite this value.
            _lastKnownServerStateId = _inventory.StateId;
        }

        /// <summary>Handles a full inventory resync from the server.</summary>
        private void OnInventorySync(ConnectionId connId, byte[] data, int offset, int length)
        {
            InventorySyncMessage msg = InventorySyncMessage.Deserialize(data, offset, length);

            ItemStack[] snapshot = new ItemStack[Inventory.SlotCount];

            for (int i = 0; i < msg.Slots.Length; i++)
            {
                SyncSlot slot = msg.Slots[i];

                if (slot.SlotIndex >= Inventory.SlotCount)
                {
                    continue;
                }

                ResourceId itemId = new(slot.Ns, slot.Name);
                snapshot[slot.SlotIndex] = new ItemStack(itemId, slot.Count, slot.Durability);
            }

            _inventory.ApplyFullSnapshot(snapshot, msg.StateId);
            _lastKnownServerStateId = msg.StateId;

            if (msg.HasCursor)
            {
                ResourceId cursorId = new(msg.CursorNs, msg.CursorName);
                ItemStack cursor = new(cursorId, msg.CursorCount, msg.CursorDurability);
                OnCursorCorrected?.Invoke(cursor);
            }
            else
            {
                OnCursorCorrected?.Invoke(ItemStack.Empty);
            }
        }

        /// <summary>Handles a targeted slot or cursor correction from the server.</summary>
        private void OnSlotUpdate(ConnectionId connId, byte[] data, int offset, int length)
        {
            InventorySlotUpdateMessage msg = InventorySlotUpdateMessage.Deserialize(data, offset, length);

            if (msg.SlotIndex == InventorySlotUpdateMessage.CursorSlotIndex)
            {
                // Cursor correction
                if (msg.Count == 0)
                {
                    OnCursorCorrected?.Invoke(ItemStack.Empty);
                }
                else
                {
                    ResourceId itemId = new(msg.Ns, msg.Name);
                    OnCursorCorrected?.Invoke(new ItemStack(itemId, msg.Count, msg.Durability));
                }
            }
            else
            {
                // Slot correction
                if (msg.Count == 0)
                {
                    _inventory.SetSlot(msg.SlotIndex, ItemStack.Empty);
                }
                else
                {
                    ResourceId itemId = new(msg.Ns, msg.Name);
                    _inventory.SetSlot(msg.SlotIndex, new ItemStack(itemId, msg.Count, msg.Durability));
                }

                _inventory.ForceStateId(msg.StateId);
                _lastKnownServerStateId = msg.StateId;
            }
        }
    }
}
