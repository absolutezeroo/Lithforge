using System;

using Lithforge.Core.Data;
using Lithforge.Item;
using Lithforge.Network;
using Lithforge.Network.Client;
using Lithforge.Network.Connection;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;

using Unity.Mathematics;

namespace Lithforge.Runtime.Network
{
    /// <summary>
    ///     Client-side handler for server inventory corrections, full resyncs, and
    ///     container session messages. Receives <see cref="InventorySyncMessage" /> (full resync),
    ///     <see cref="InventorySlotUpdateMessage" /> (targeted slot/cursor corrections),
    ///     <see cref="ContainerOpenMessage" />, <see cref="ContainerCloseMessage" />, and
    ///     <see cref="ContainerProgressMessage" />.
    ///     Also provides send methods for slot clicks, container open/close, and craft actions.
    /// </summary>
    public sealed class ClientInventorySyncHandler
    {
        /// <summary>The client-side player inventory to apply corrections to.</summary>
        private readonly Inventory _inventory;

        /// <summary>Network client for sending commands to the server.</summary>
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

        /// <summary>
        ///     Fired when the server opens a container session. Parameter is the full
        ///     <see cref="ContainerOpenMessage" /> with WindowId, EntityTypeId, Slots, and position.
        /// </summary>
        public Action<ContainerOpenMessage> OnContainerOpened;

        /// <summary>
        ///     Fired when the server force-closes a container session. Parameter is the WindowId.
        /// </summary>
        public Action<byte> OnContainerClosed;

        /// <summary>
        ///     Fired when the server sends furnace progress updates.
        ///     Parameters: WindowId, BurnProgress (0–65535), SmeltProgress (0–65535).
        /// </summary>
        public Action<byte, ushort, ushort> OnContainerProgress;

        /// <summary>
        ///     Fired when the server sends a container slot update (WindowId > 0).
        ///     Parameters: slotIndex, itemStack.
        /// </summary>
        public Action<int, ItemStack> OnContainerSlotUpdated;

        /// <summary>The active container window ID, or 0 if no container is open.</summary>
        public byte ActiveWindowId { get; private set; }

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
            dispatcher.RegisterHandler(MessageType.ContainerOpen, OnContainerOpen);
            dispatcher.RegisterHandler(MessageType.ContainerClose, OnContainerClose);
            dispatcher.RegisterHandler(MessageType.ContainerProgress, OnContainerProgressMsg);
        }

        /// <summary>
        ///     Sends a slot click command to the server. Called by container screens
        ///     after local prediction executes. Uses the last known server state ID
        ///     (not the local post-prediction value) so the server can match it
        ///     against its authoritative state.
        /// </summary>
        public void SendSlotClick(int slotIndex, byte clickType, byte button, byte windowId = 0)
        {
            SlotClickCmdMessage msg = new()
            {
                WindowId = windowId,
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

        /// <summary>Sends a container open command to the server for the given world block position.</summary>
        public void SendContainerOpen(int3 position)
        {
            ContainerOpenCmdMessage msg = new()
            {
                PositionX = position.x,
                PositionY = position.y,
                PositionZ = position.z,
            };

            _client.Send(msg, PipelineId.ReliableSequenced);
        }

        /// <summary>Sends a container close command to the server for the given window ID.</summary>
        public void SendContainerClose(byte windowId)
        {
            ContainerCloseCmdMessage msg = new()
            {
                WindowId = windowId,
            };

            _client.Send(msg, PipelineId.ReliableSequenced);
            ActiveWindowId = 0;
        }

        /// <summary>Sends a craft action command to the server with the recipe ID.</summary>
        public void SendCraftAction(ResourceId recipeId, bool shiftClick)
        {
            CraftActionCmdMessage msg = new()
            {
                WindowId = ActiveWindowId,
                IsShiftClick = (byte)(shiftClick ? 1 : 0),
                RecipeNs = recipeId.Namespace,
                RecipeName = recipeId.Name,
            };

            _client.Send(msg, PipelineId.ReliableSequenced);
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
            else if (msg.WindowId > 0)
            {
                // Container slot correction — route to container handler
                ItemStack stack = msg.Count == 0
                    ? ItemStack.Empty
                    : new ItemStack(new ResourceId(msg.Ns, msg.Name), msg.Count, msg.Durability);

                OnContainerSlotUpdated?.Invoke(msg.SlotIndex, stack);
            }
            else
            {
                // Player inventory slot correction
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

        /// <summary>Handles a container open message from the server.</summary>
        private void OnContainerOpen(ConnectionId connId, byte[] data, int offset, int length)
        {
            ContainerOpenMessage msg = ContainerOpenMessage.Deserialize(data, offset, length);
            ActiveWindowId = msg.WindowId;
            OnContainerOpened?.Invoke(msg);
        }

        /// <summary>Handles a container close (force-close) message from the server.</summary>
        private void OnContainerClose(ConnectionId connId, byte[] data, int offset, int length)
        {
            ContainerCloseMessage msg = ContainerCloseMessage.Deserialize(data, offset, length);

            if (msg.WindowId == ActiveWindowId)
            {
                ActiveWindowId = 0;
            }

            OnContainerClosed?.Invoke(msg.WindowId);
        }

        /// <summary>Handles a container progress (furnace burn/smelt) message from the server.</summary>
        private void OnContainerProgressMsg(ConnectionId connId, byte[] data, int offset, int length)
        {
            ContainerProgressMessage msg = ContainerProgressMessage.Deserialize(data, offset, length);
            OnContainerProgress?.Invoke(msg.WindowId, msg.BurnProgress, msg.SmeltProgress);
        }
    }
}
