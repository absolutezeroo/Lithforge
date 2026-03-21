using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Item;
using Lithforge.Item.Crafting;
using Lithforge.Item.Interaction;
using Lithforge.Network.Connection;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Command;
using Lithforge.Voxel.Storage;

using Unity.Mathematics;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Server-authoritative inventory processor. Holds per-player inventories,
    ///     re-executes slot click commands via <see cref="SlotActionExecutor" />,
    ///     reconciles against client predictions, broadcasts per-tick deltas,
    ///     manages server-side crafting grids, and orchestrates container sessions
    ///     for block entity containers (chests, furnaces, etc.).
    /// </summary>
    public sealed class ServerInventoryProcessor
    {
        /// <summary>Maximum number of items a shift-craft loop can produce.</summary>
        private const int MaxShiftCraftIterations = 64;

        /// <summary>Per-player inventories keyed by player ID.</summary>
        private readonly Dictionary<ushort, Inventory> _inventories = new();

        /// <summary>Per-player server-side crafting grids (created when crafting screen opens).</summary>
        private readonly Dictionary<ushort, CraftingGrid> _playerCraftGrids = new();

        /// <summary>Item registry for looking up max stack sizes.</summary>
        private readonly ItemRegistry _itemRegistry;

        /// <summary>Network server for sending correction messages.</summary>
        private readonly INetworkServer _server;

        /// <summary>Concrete server for peer lookup.</summary>
        private readonly NetworkServer _serverImpl;

        /// <summary>Crafting engine for recipe matching (null until set).</summary>
        private CraftingEngine _craftingEngine;

        /// <summary>Container session manager (null until set).</summary>
        private ServerContainerManager _containerManager;

        /// <summary>Block entity provider for container access (null until set).</summary>
        private IServerBlockEntityProvider _blockEntityProvider;

        /// <summary>Simulation for player position queries (null until set).</summary>
        private IServerSimulation _simulation;

        /// <summary>Reusable list for entity cleanup iteration.</summary>
        private readonly List<ContainerSession> _tempClosedSessions = new();

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

        /// <summary>Injects the crafting engine for server-side recipe validation.</summary>
        public void SetCraftingEngine(CraftingEngine craftingEngine)
        {
            _craftingEngine = craftingEngine;
        }

        /// <summary>Injects the container manager and block entity provider for container sessions.</summary>
        public void SetContainerDependencies(
            ServerContainerManager containerManager,
            IServerBlockEntityProvider blockEntityProvider,
            IServerSimulation simulation)
        {
            _containerManager = containerManager;
            _blockEntityProvider = blockEntityProvider;
            _simulation = simulation;
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
        ///     When WindowId > 0, routes the click to the active container session's storage.
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

            // Container slot click (WindowId > 0): route to container session
            if (cmd.WindowId > 0)
            {
                ProcessContainerSlotClick(peer, inventory, ref cursor, cmd);

                if (peer.InterestState is not null)
                {
                    peer.InterestState.ServerCursor = cursor;
                }

                return null;
            }

            // Crafting output take (ClickType=5): server re-executes recipe match
            if (cmd.ClickType == SlotActionExecutor.ClickOutputTake)
            {
                // No crafting engine or no grid → reject silently (client will self-correct)
                if (_craftingEngine is null)
                {
                    return null;
                }

                // Crafting output from player inventory's 2x2 grid is WindowId=0
                // For now, reject ClickType=5 on WindowId=0 (2x2 crafting not yet wired)
                // The client should use CraftActionCmd for all crafting
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
            SyncRemoteStateForResult(peer, inventory, cursor, result);

            return null;
        }

        /// <summary>
        ///     Processes a craft action command from a client. Server re-executes the
        ///     recipe match on the server-side crafting grid, verifies the recipe ID,
        ///     and atomically consumes ingredients + produces result.
        /// </summary>
        public void ProcessCraftAction(PeerInfo peer, CraftActionCmdMessage cmd)
        {
            if (_craftingEngine is null)
            {
                return;
            }

            ushort playerId = peer.AssignedPlayerId;

            if (!_inventories.TryGetValue(playerId, out Inventory inventory))
            {
                return;
            }

            if (!_playerCraftGrids.TryGetValue(playerId, out CraftingGrid grid))
            {
                return;
            }

            ItemStack cursor = peer.InterestState?.ServerCursor ?? ItemStack.Empty;

            if (cmd.IsShiftClick != 0)
            {
                ProcessShiftCraftAction(peer, inventory, grid, ref cursor);
            }
            else
            {
                ProcessSingleCraftAction(peer, inventory, grid, ref cursor, cmd);
            }

            if (peer.InterestState is not null)
            {
                peer.InterestState.ServerCursor = cursor;
            }
        }

        /// <summary>
        ///     Processes a container open command from a client. Validates distance
        ///     and entity existence, creates a session, and sends ContainerOpenMessage.
        /// </summary>
        public void ProcessContainerOpen(PeerInfo peer, ContainerOpenCmdMessage cmd)
        {
            if (_containerManager is null || _blockEntityProvider is null || _simulation is null)
            {
                return;
            }

            ushort playerId = peer.AssignedPlayerId;
            int3 worldPos = new(cmd.PositionX, cmd.PositionY, cmd.PositionZ);

            // Convert world position to chunk coord + flat index
            int3 chunkCoord = new(
                (int)math.floor((float)worldPos.x / ChunkConstants.Size),
                (int)math.floor((float)worldPos.y / ChunkConstants.Size),
                (int)math.floor((float)worldPos.z / ChunkConstants.Size));

            int localX = worldPos.x - chunkCoord.x * ChunkConstants.Size;
            int localY = worldPos.y - chunkCoord.y * ChunkConstants.Size;
            int localZ = worldPos.z - chunkCoord.z * ChunkConstants.Size;

            // Handle negative modulo
            if (localX < 0) { localX += ChunkConstants.Size; }
            if (localY < 0) { localY += ChunkConstants.Size; }
            if (localZ < 0) { localZ += ChunkConstants.Size; }

            int flatIndex = localY * ChunkConstants.Size * ChunkConstants.Size
                          + localZ * ChunkConstants.Size + localX;

            // Validate entity exists
            if (!_blockEntityProvider.EntityExists(chunkCoord, flatIndex))
            {
                return;
            }

            // Validate distance
            PlayerPhysicsState physState = _simulation.GetPlayerState(playerId);

            if (!ServerContainerManager.IsWithinReach(physState.Position, worldPos))
            {
                return;
            }

            string entityTypeId = _blockEntityProvider.GetEntityTypeId(chunkCoord, flatIndex);
            IItemStorage storage = _blockEntityProvider.GetEntityInventory(chunkCoord, flatIndex);

            if (storage is null || entityTypeId is null)
            {
                return;
            }

            ContainerSession session = _containerManager.OpenSession(
                playerId, chunkCoord, flatIndex, entityTypeId, storage);

            if (session is null)
            {
                return;
            }

            // Build and send ContainerOpenMessage
            int slotCount = storage.SlotCount;
            int nonEmpty = 0;

            for (int i = 0; i < slotCount; i++)
            {
                if (!storage.GetSlot(i).IsEmpty)
                {
                    nonEmpty++;
                }
            }

            SyncSlot[] slots = new SyncSlot[nonEmpty];
            int writeIdx = 0;

            for (int i = 0; i < slotCount; i++)
            {
                ItemStack stack = storage.GetSlot(i);

                if (stack.IsEmpty)
                {
                    continue;
                }

                slots[writeIdx++] = new SyncSlot
                {
                    SlotIndex = (byte)i,
                    Ns = stack.ItemId.Namespace,
                    Name = stack.ItemId.Name,
                    Count = (ushort)stack.Count,
                    Durability = (short)stack.Durability,
                };
            }

            ContainerOpenMessage openMsg = new()
            {
                WindowId = session.WindowId,
                EntityTypeId = entityTypeId,
                PositionX = worldPos.x,
                PositionY = worldPos.y,
                PositionZ = worldPos.z,
                Slots = slots,
            };

            _server.SendTo(peer.ConnectionId, openMsg, PipelineId.ReliableSequenced);

            // Mark all slots as sent in remote state
            session.RemoteState.SyncAll(storage);
        }

        /// <summary>
        ///     Processes a container close command from a client. Returns cursor items
        ///     to the player inventory and closes the session.
        /// </summary>
        public void ProcessContainerClose(PeerInfo peer, ContainerCloseCmdMessage cmd)
        {
            if (_containerManager is null)
            {
                return;
            }

            ushort playerId = peer.AssignedPlayerId;
            ContainerSession session = _containerManager.CloseSession(playerId);

            if (session is null)
            {
                return;
            }

            // Return session cursor to player inventory (if any)
            ReturnSessionCursor(playerId, session);

            // If this was a crafting session, return grid items too
            ReturnCraftingGridItems(playerId);
        }

        /// <summary>
        ///     Called when a block entity is removed (broken). Force-closes all container
        ///     sessions viewing that entity, returns cursors, and sends close messages.
        /// </summary>
        public void OnBlockEntityRemoved(int3 chunkCoord, int flatIndex)
        {
            if (_containerManager is null)
            {
                return;
            }

            _containerManager.CloseAllForEntity(chunkCoord, flatIndex, _tempClosedSessions);

            for (int i = 0; i < _tempClosedSessions.Count; i++)
            {
                ContainerSession session = _tempClosedSessions[i];
                ReturnSessionCursor(session.PlayerId, session);

                PeerInfo peer = FindPeerByPlayerId(session.PlayerId);

                if (peer is not null)
                {
                    ContainerCloseMessage closeMsg = new()
                    {
                        WindowId = session.WindowId,
                    };

                    _server.SendTo(peer.ConnectionId, closeMsg, PipelineId.ReliableSequenced);
                }
            }
        }

        /// <summary>
        ///     Handles player disconnect: closes container session, returns cursor,
        ///     returns crafting grid items.
        /// </summary>
        public void CleanupPlayerContainers(ushort playerId)
        {
            if (_containerManager is not null)
            {
                ContainerSession session = _containerManager.CloseAllForPlayer(playerId);

                if (session is not null)
                {
                    ReturnSessionCursor(playerId, session);
                }
            }

            ReturnCraftingGridItems(playerId);
        }

        /// <summary>
        ///     Creates or retrieves the server-side crafting grid for a player.
        ///     Called when a crafting screen (3x3) opens on the server.
        /// </summary>
        public CraftingGrid GetOrCreateCraftingGrid(ushort playerId, int width, int height)
        {
            if (!_playerCraftGrids.TryGetValue(playerId, out CraftingGrid grid))
            {
                grid = new CraftingGrid(width, height);
                _playerCraftGrids[playerId] = grid;
            }

            return grid;
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
        ///     Per-tick container delta sync. Compares each open container session's
        ///     current storage against what was last sent, sending slot updates and
        ///     progress updates for furnaces. Also validates distance and entity existence,
        ///     force-closing invalid sessions.
        /// </summary>
        public void BroadcastContainerDeltas()
        {
            if (_containerManager is null || _blockEntityProvider is null || _simulation is null)
            {
                return;
            }

            // Iterate a snapshot to avoid modification during iteration
            _tempClosedSessions.Clear();

            foreach (ContainerSession session in _containerManager.GetAllSessions())
            {
                PeerInfo peer = FindPeerByPlayerId(session.PlayerId);

                if (peer is null)
                {
                    _tempClosedSessions.Add(session);
                    continue;
                }

                // Validate entity still exists and player is within reach
                if (!_blockEntityProvider.EntityExists(session.ChunkCoord, session.FlatIndex))
                {
                    _tempClosedSessions.Add(session);
                    continue;
                }

                PlayerPhysicsState physState = _simulation.GetPlayerState(session.PlayerId);
                int3 worldPos = session.ChunkCoord * ChunkConstants.Size
                              + new int3(
                                  session.FlatIndex % ChunkConstants.Size,
                                  session.FlatIndex / (ChunkConstants.Size * ChunkConstants.Size),
                                  (session.FlatIndex / ChunkConstants.Size) % ChunkConstants.Size);

                if (!ServerContainerManager.IsWithinReach(physState.Position, worldPos))
                {
                    _tempClosedSessions.Add(session);
                    continue;
                }

                // Diff container slots
                IItemStorage storage = session.Storage;
                ContainerRemoteState remoteState = session.RemoteState;

                if (!_inventories.TryGetValue(session.PlayerId, out Inventory inventory))
                {
                    continue;
                }

                uint stateId = inventory.StateId;

                for (int slot = 0; slot < storage.SlotCount; slot++)
                {
                    ItemStack current = storage.GetSlot(slot);

                    if (!remoteState.IsSlotDirty(slot, current))
                    {
                        continue;
                    }

                    InventorySlotUpdateMessage update = new()
                    {
                        WindowId = session.WindowId,
                        StateId = stateId,
                        SlotIndex = (short)slot,
                        Count = (ushort)current.Count,
                    };

                    if (!current.IsEmpty)
                    {
                        update.Ns = current.ItemId.Namespace;
                        update.Name = current.ItemId.Name;
                        update.Durability = (short)current.Durability;
                    }

                    _server.SendTo(peer.ConnectionId, update, PipelineId.ReliableSequenced);
                    remoteState.MarkSlotSent(slot, current);
                }

                // Furnace progress updates
                if (session.EntityTypeId is "lithforge:furnace")
                {
                    float burnF = _blockEntityProvider.GetFurnaceBurnProgress(
                        session.ChunkCoord, session.FlatIndex);
                    float smeltF = _blockEntityProvider.GetFurnaceSmeltProgress(
                        session.ChunkCoord, session.FlatIndex);

                    ushort burn = (ushort)(burnF * 65535f);
                    ushort smelt = (ushort)(smeltF * 65535f);

                    if (burn != session.LastSentBurnProgress || smelt != session.LastSentSmeltProgress)
                    {
                        ContainerProgressMessage progress = new()
                        {
                            WindowId = session.WindowId,
                            BurnProgress = burn,
                            SmeltProgress = smelt,
                        };

                        _server.SendTo(peer.ConnectionId, progress, PipelineId.ReliableSequenced);
                        session.LastSentBurnProgress = burn;
                        session.LastSentSmeltProgress = smelt;
                    }
                }
            }

            // Force-close invalid sessions
            for (int i = 0; i < _tempClosedSessions.Count; i++)
            {
                ContainerSession session = _tempClosedSessions[i];
                _containerManager.CloseSession(session.PlayerId);
                ReturnSessionCursor(session.PlayerId, session);

                PeerInfo peer = FindPeerByPlayerId(session.PlayerId);

                if (peer is not null)
                {
                    ContainerCloseMessage closeMsg = new()
                    {
                        WindowId = session.WindowId,
                    };

                    _server.SendTo(peer.ConnectionId, closeMsg, PipelineId.ReliableSequenced);
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

            ReturnItemToInventory(inventory, cursor);

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

        /// <summary>Processes a container slot click, routing to the session's IItemStorage.</summary>
        private void ProcessContainerSlotClick(
            PeerInfo peer,
            Inventory inventory,
            ref ItemStack cursor,
            SlotClickCmdMessage cmd)
        {
            if (_containerManager is null)
            {
                return;
            }

            ContainerSession session = _containerManager.GetSession(peer.AssignedPlayerId);

            if (session is null || session.WindowId != cmd.WindowId)
            {
                return;
            }

            IItemStorage storage = session.Storage;

            if (cmd.SlotIndex < 0 || cmd.SlotIndex >= storage.SlotCount)
            {
                return;
            }

            // Delegate to SlotActionExecutor with hotbarSize=0 (no shift-click/number-key in containers)
            SlotActionExecutor.Execute(
                storage, ref cursor, cmd.SlotIndex, cmd.ClickType, cmd.Button, _itemRegistry, 0);

            inventory.IncrementStateId();
        }

        /// <summary>Processes a single crafting action (non-shift).</summary>
        private void ProcessSingleCraftAction(
            PeerInfo peer,
            Inventory inventory,
            CraftingGrid grid,
            ref ItemStack cursor,
            CraftActionCmdMessage cmd)
        {
            RecipeEntry match = _craftingEngine.FindMatch(grid);

            if (match is null)
            {
                // No match — send full resync to correct client
                SendFullResyncForPlayer(peer, inventory, cursor);
                return;
            }

            // Verify recipe ID matches what the client sent
            ResourceId clientRecipeId = new(cmd.RecipeNs ?? "", cmd.RecipeName ?? "");

            if (match.Id != clientRecipeId)
            {
                SendFullResyncForPlayer(peer, inventory, cursor);
                return;
            }

            // Verify cursor can accept the result
            if (!cursor.IsEmpty)
            {
                if (!ItemStack.CanStack(cursor, new ItemStack(match.ResultItem, 1)))
                {
                    return;
                }

                ItemEntry def = _itemRegistry.Get(match.ResultItem);
                int maxStack = def?.MaxStackSize ?? 64;

                if (cursor.Count + match.ResultCount > maxStack)
                {
                    return;
                }
            }

            // Consume ingredients (decrement each non-empty grid slot by 1)
            ConsumeGridIngredients(grid);

            // Produce result to cursor
            if (cursor.IsEmpty)
            {
                cursor = new ItemStack(match.ResultItem, match.ResultCount);
            }
            else
            {
                ItemStack updated = cursor;
                updated.Count += match.ResultCount;
                cursor = updated;
            }

            inventory.IncrementStateId();
        }

        /// <summary>Processes a shift-craft action (craft all up to 64 iterations).</summary>
        private void ProcessShiftCraftAction(
            PeerInfo peer,
            Inventory inventory,
            CraftingGrid grid,
            ref ItemStack cursor)
        {
            for (int iteration = 0; iteration < MaxShiftCraftIterations; iteration++)
            {
                RecipeEntry match = _craftingEngine.FindMatch(grid);

                if (match is null)
                {
                    break;
                }

                // Check if the result fits in the player inventory
                ItemEntry def = _itemRegistry.Get(match.ResultItem);
                int maxStack = def?.MaxStackSize ?? 64;

                if (!inventory.CanAdd(match.ResultItem, match.ResultCount, maxStack))
                {
                    break;
                }

                // Consume ingredients
                ConsumeGridIngredients(grid);

                // Add result directly to inventory
                inventory.AddItem(match.ResultItem, match.ResultCount, maxStack);
            }
        }

        /// <summary>Decrements each non-empty slot in the crafting grid by 1.</summary>
        private static void ConsumeGridIngredients(CraftingGrid grid)
        {
            for (int i = 0; i < grid.SlotCount; i++)
            {
                ItemStack slot = grid.GetSlot(i);

                if (slot.IsEmpty)
                {
                    continue;
                }

                if (slot.Count <= 1)
                {
                    grid.SetSlot(i, ItemStack.Empty);
                }
                else
                {
                    ItemStack updated = slot;
                    updated.Count -= 1;
                    grid.SetSlot(i, updated);
                }
            }
        }

        /// <summary>Returns all items from a player's crafting grid to their inventory.</summary>
        private void ReturnCraftingGridItems(ushort playerId)
        {
            if (!_playerCraftGrids.TryGetValue(playerId, out CraftingGrid grid))
            {
                return;
            }

            if (!_inventories.TryGetValue(playerId, out Inventory inventory))
            {
                _playerCraftGrids.Remove(playerId);
                return;
            }

            for (int i = 0; i < grid.SlotCount; i++)
            {
                ItemStack slot = grid.GetSlot(i);

                if (slot.IsEmpty)
                {
                    continue;
                }

                ReturnItemToInventory(inventory, slot);
                grid.SetSlot(i, ItemStack.Empty);
            }

            _playerCraftGrids.Remove(playerId);
        }

        /// <summary>Returns a container session cursor to the player's inventory.</summary>
        private void ReturnSessionCursor(ushort playerId, ContainerSession session)
        {
            if (session.Cursor.IsEmpty)
            {
                return;
            }

            if (!_inventories.TryGetValue(playerId, out Inventory inventory))
            {
                return;
            }

            ReturnItemToInventory(inventory, session.Cursor);
            session.Cursor = ItemStack.Empty;
        }

        /// <summary>Returns an item stack to the player's inventory, handling tools and components.</summary>
        private void ReturnItemToInventory(Inventory inventory, ItemStack stack)
        {
            ItemEntry def = _itemRegistry.Get(stack.ItemId);
            int maxStack = def?.MaxStackSize ?? 64;

            if (stack.HasComponents)
            {
                inventory.AddItemStack(stack);
            }
            else
            {
                inventory.AddItem(stack.ItemId, stack.Count, maxStack);
            }
        }

        /// <summary>Sends a full resync to bring the client back in sync.</summary>
        private void SendFullResyncForPlayer(PeerInfo peer, Inventory inventory, ItemStack cursor)
        {
            InventorySyncMessage resync = BuildFullSync(inventory, cursor);
            _server.SendTo(peer.ConnectionId, resync, PipelineId.ReliableSequenced);

            if (peer.InterestState?.InventoryRemote is not null)
            {
                peer.InterestState.InventoryRemote.SyncAll(inventory, cursor);
            }
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
