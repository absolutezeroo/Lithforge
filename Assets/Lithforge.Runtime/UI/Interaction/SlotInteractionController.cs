using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Runtime.Content.Tools;
using Lithforge.Runtime.UI.Container;
using Lithforge.Voxel.Crafting;
using Lithforge.Voxel.Item;

using UnityEngine;

namespace Lithforge.Runtime.UI.Interaction
{
    /// <summary>
    ///     Screen-agnostic controller for all slot interaction modes:
    ///     1. Left-click:   pick up / place / merge / swap
    ///     2. Right-click:  pick up half / place 1
    ///     3. Shift+click:  quick transfer between hotbar and main
    ///     4. Paint-drag:   right-click drag to distribute 1 item per slot
    ///     5. Number keys:  swap hovered slot with hotbar slot
    ///     6. Output take:  consume craft ingredients and collect result
    ///     Operates entirely on ISlotContainer — no knowledge of specific screen layout.
    /// </summary>
    public sealed class SlotInteractionController
    {
        private readonly ItemRegistry _itemRegistry;
        private readonly HashSet<long> _paintedSlots = new();
        private readonly ToolMaterialRegistry _toolMaterialRegistry;
        private readonly ToolTemplateRegistry _toolTemplateRegistry;

        // Hover tracking

        // Paint mode state
        private bool _isPainting;
        private DataComponentMap _paintComponents;
        private int _paintDurability;
        private ResourceId _paintItemId;
        private ISlotContainer _paintOriginContainer;
        private int _paintOriginSlot;
        private bool _paintPending;

        public SlotInteractionController(
            HeldStack held,
            ItemRegistry itemRegistry,
            ToolTemplateRegistry toolTemplateRegistry,
            ToolMaterialRegistry toolMaterialRegistry)
        {
            Held = held;
            _itemRegistry = itemRegistry;
            _toolTemplateRegistry = toolTemplateRegistry;
            _toolMaterialRegistry = toolMaterialRegistry;
        }

        public HeldStack Held { get; }

        public int HoveredSlotIndex { get; private set; } = -1;

        public ISlotContainer HoveredContainer { get; private set; }

        // ────────────────────────────────────────────────────────────────────
        // Hover tracking
        // ────────────────────────────────────────────────────────────────────

        public void OnSlotEnter(ISlotContainer container, int slotIndex)
        {
            HoveredContainer = container;
            HoveredSlotIndex = slotIndex;
            HandlePaintHover(container, slotIndex);
        }

        public void OnSlotLeave(ISlotContainer container, int slotIndex)
        {
            if (HoveredContainer == container && HoveredSlotIndex == slotIndex)
            {
                HoveredContainer = null;
                HoveredSlotIndex = -1;
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Left-click
        // ────────────────────────────────────────────────────────────────────

        public void LeftClick(ISlotContainer container, int slotIndex)
        {
            if (container.IsReadOnly)
            {
                return;
            }

            ItemStack slotItem = container.GetSlot(slotIndex);
            int slotMaxStack = GetMaxStack(slotItem);

            if (Held.IsEmpty && slotItem.IsEmpty)
            {
                return;
            }

            if (Held.IsEmpty)
            {
                // Pick up entire stack
                Held.Set(slotItem);
                container.SetSlot(slotIndex, ItemStack.Empty);
            }
            else if (slotItem.IsEmpty)
            {
                // Place entire held stack
                container.SetSlot(slotIndex, Held.Stack);
                Held.Clear();
            }
            else if (ItemStack.CanStack(Held.Stack, slotItem))
            {
                // Merge held into slot
                int space = slotMaxStack - slotItem.Count;

                if (space <= 0)
                {
                    // Slot full, swap
                    container.SetSlot(slotIndex, Held.Stack);
                    Held.Set(slotItem);
                }
                else
                {
                    int toMove = Mathf.Min(Held.Stack.Count, space);
                    ItemStack newSlot = slotItem;
                    newSlot.Count += toMove;
                    container.SetSlot(slotIndex, newSlot);

                    ItemStack newHeld = Held.Stack;
                    newHeld.Count -= toMove;
                    Held.Set(newHeld.IsEmpty ? ItemStack.Empty : newHeld);
                }
            }
            else
            {
                // Swap different items
                container.SetSlot(slotIndex, Held.Stack);
                Held.Set(slotItem);
            }

            container.OnSlotChanged(slotIndex);
        }

        // ────────────────────────────────────────────────────────────────────
        // Right-click
        // ────────────────────────────────────────────────────────────────────

        public void RightClick(ISlotContainer container, int slotIndex)
        {
            if (container.IsReadOnly)
            {
                return;
            }

            ItemStack slotItem = container.GetSlot(slotIndex);
            int slotMaxStack = GetMaxStack(slotItem);

            // Repair kit on tool: TiC-style inventory repair
            if (!Held.IsEmpty && !slotItem.IsEmpty && _toolMaterialRegistry != null)
            {
                ToolPartDataComponent kitComp = Held.Stack.Components?.Get<ToolPartDataComponent>(
                    DataComponentTypes.ToolPartDataId);

                if (kitComp != null && kitComp.PartData.PartType == ToolPartType.RepairKit)
                {
                    ToolInstanceComponent toolComp = slotItem.Components?.Get<ToolInstanceComponent>(
                        DataComponentTypes.ToolInstanceId);

                    if (toolComp != null)
                    {
                        ToolInstance tool = toolComp.Tool;
                        int repairAmount = RepairKitHelper.CalculateRepairKitRepair(
                            tool, kitComp.PartData, _toolMaterialRegistry);

                        if (repairAmount > 0)
                        {
                            tool.SetCurrentDurability(
                                (tool.IsBroken ? 0 : tool.CurrentDurability) + repairAmount);

                            ItemStack repairedStack = slotItem;
                            repairedStack.Durability = tool.CurrentDurability;
                            DataComponentMap repairedMap = new();
                            repairedMap.Set(DataComponentTypes.ToolInstanceId,
                                new ToolInstanceComponent(tool));
                            repairedStack.Components = repairedMap;
                            container.SetSlot(slotIndex, repairedStack);

                            ItemStack newHeld = Held.Stack;
                            newHeld.Count -= 1;
                            Held.Set(newHeld.IsEmpty ? ItemStack.Empty : newHeld);

                            container.OnSlotChanged(slotIndex);
                            return;
                        }
                    }
                }
            }

            if (Held.IsEmpty && slotItem.IsEmpty)
            {
                return;
            }

            if (Held.IsEmpty)
            {
                // Pick up half (rounded up)
                int half = (slotItem.Count + 1) / 2;
                ItemStack pickup = new(slotItem.ItemId, half, slotItem.Durability);
                pickup.Components = slotItem.Components;
                Held.Set(pickup);

                ItemStack remaining = slotItem;
                remaining.Count -= half;
                container.SetSlot(slotIndex, remaining.IsEmpty ? ItemStack.Empty : remaining);
            }
            else if (slotItem.IsEmpty)
            {
                // Place 1
                ItemStack placed = new(Held.Stack.ItemId, 1, Held.Stack.Durability);
                placed.Components = Held.Stack.Components;
                container.SetSlot(slotIndex, placed);

                ItemStack newHeld = Held.Stack;
                newHeld.Count -= 1;
                Held.Set(newHeld.IsEmpty ? ItemStack.Empty : newHeld);

                if (!Held.IsEmpty)
                {
                    StartPaint(container, slotIndex);
                }
            }
            else if (ItemStack.CanStack(Held.Stack, slotItem) && slotItem.Count < slotMaxStack)
            {
                // Place 1 on same item
                ItemStack newSlot = slotItem;
                newSlot.Count += 1;
                container.SetSlot(slotIndex, newSlot);

                ItemStack newHeld = Held.Stack;
                newHeld.Count -= 1;
                Held.Set(newHeld.IsEmpty ? ItemStack.Empty : newHeld);

                if (!Held.IsEmpty)
                {
                    StartPaint(container, slotIndex);
                }
            }
            // Right click on different item = nothing (Minecraft behavior)

            container.OnSlotChanged(slotIndex);
        }

        // ────────────────────────────────────────────────────────────────────
        // Shift+click quick transfer
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        ///     Quick-transfers a slot between hotbar and main inventory adapters.
        /// </summary>
        public void ShiftClick(
            ISlotContainer source,
            int slotIndex,
            InventoryContainerAdapter hotbarAdapter,
            InventoryContainerAdapter mainAdapter)
        {
            ItemStack slotItem = source.GetSlot(slotIndex);

            if (slotItem.IsEmpty)
            {
                return;
            }

            int maxStack = GetMaxStack(slotItem);

            // Determine target: if source is hotbar, send to main; otherwise send to hotbar
            ISlotContainer target;

            if (source == hotbarAdapter)
            {
                target = mainAdapter;
            }
            else
            {
                target = hotbarAdapter;
            }

            int remaining = TransferToContainer(slotItem, target, maxStack);

            if (remaining > 0)
            {
                ItemStack remainder = new(slotItem.ItemId, remaining, slotItem.Durability);
                remainder.Components = slotItem.Components;
                source.SetSlot(slotIndex, remainder);
            }
            else
            {
                source.SetSlot(slotIndex, ItemStack.Empty);
            }

            source.OnSlotChanged(slotIndex);
        }

        // ────────────────────────────────────────────────────────────────────
        // Number key swap
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        ///     Swaps the hovered inventory slot with the given hotbar slot index.
        /// </summary>
        public void NumberKeySwap(
            ISlotContainer hoveredContainer,
            int hoveredIndex,
            InventoryContainerAdapter hotbarAdapter,
            int hotbarSlot)
        {
            if (hoveredContainer == null || hoveredIndex < 0)
            {
                return;
            }

            // Don't swap with self
            if (hoveredContainer == hotbarAdapter && hoveredIndex == hotbarSlot)
            {
                return;
            }

            ItemStack hotbarItem = hotbarAdapter.GetSlot(hotbarSlot);
            ItemStack hoveredItem = hoveredContainer.GetSlot(hoveredIndex);

            hotbarAdapter.SetSlot(hotbarSlot, hoveredItem);
            hoveredContainer.SetSlot(hoveredIndex, hotbarItem);
            hotbarAdapter.OnSlotChanged(hotbarSlot);
            hoveredContainer.OnSlotChanged(hoveredIndex);
        }

        // ────────────────────────────────────────────────────────────────────
        // Output take (craft result)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        ///     Takes the crafting output. Adds result to held stack.
        /// </summary>
        public void TakeOutput(CraftingOutputContainerAdapter output, CraftingGridContainerAdapter grid)
        {
            if (output.CurrentMatch == null)
            {
                return;
            }

            ItemStack result = output.GetSlot(0);

            if (Held.IsEmpty)
            {
                Held.Set(result);
            }
            else if (ItemStack.CanStack(Held.Stack, result))
            {
                int maxStack = GetMaxStack(Held.Stack);

                if (Held.Stack.Count + result.Count <= maxStack)
                {
                    ItemStack updated = Held.Stack;
                    updated.Count += result.Count;
                    Held.Set(updated);
                }
                else
                {
                    return; // Can't fit
                }
            }
            else
            {
                return; // Different item
            }

            output.TakeOutput(grid);
        }

        /// <summary>
        ///     Shift+click on output: craft as many as possible and send to inventory.
        /// </summary>
        public void ShiftClickOutput(
            CraftingOutputContainerAdapter output,
            CraftingGridContainerAdapter grid,
            Inventory inventory)
        {
            for (int i = 0; i < 64; i++)
            {
                if (output.CurrentMatch == null)
                {
                    break;
                }

                RecipeEntry match = output.CurrentMatch;
                ItemEntry resultDef = _itemRegistry.Get(match.ResultItem);
                int maxStack = resultDef != null ? resultDef.MaxStackSize : 64;

                byte[] toolData = _toolTemplateRegistry != null
                    ? _toolTemplateRegistry.GetTemplate(match.ResultItem)
                    : null;

                if (toolData != null)
                {
                    ToolInstance tool = ToolInstanceSerializer.Deserialize(toolData);
                    int durability = tool != null ? tool.MaxDurability : -1;
                    ItemStack resultStack = new(match.ResultItem, 1, durability);
                    DataComponentMap toolMap = new();
                    toolMap.Set(DataComponentTypes.ToolInstanceId,
                        new ToolInstanceComponent(tool));
                    resultStack.Components = toolMap;
                    int leftOver = inventory.AddItemStack(resultStack);

                    if (leftOver > 0)
                    {
                        break;
                    }
                }
                else
                {
                    int leftOver = inventory.AddItem(match.ResultItem, match.ResultCount, maxStack);

                    if (leftOver > 0)
                    {
                        break;
                    }
                }

                output.TakeOutput(grid);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Paint mode
        // ────────────────────────────────────────────────────────────────────

        public void OnPointerUp(int button)
        {
            if (button == 1)
            {
                _isPainting = false;
                _paintPending = false;
                _paintedSlots.Clear();
            }
        }

        /// <summary>
        ///     Returns held items to inventory on screen close.
        /// </summary>
        public void ReturnHeldToInventory(Inventory inventory)
        {
            if (Held.IsEmpty)
            {
                return;
            }

            ItemStack heldStack = Held.Stack;
            ItemEntry def = _itemRegistry.Get(heldStack.ItemId);
            int maxStack = def != null ? def.MaxStackSize : 64;

            if (heldStack.HasComponents)
            {
                int leftOver = inventory.AddItemStack(heldStack);
                Held.Set(leftOver == 0 ? ItemStack.Empty : heldStack);
            }
            else if (heldStack.Durability > 0)
            {
                int leftOver = inventory.AddItemWithDurability(heldStack.ItemId, heldStack.Durability);
                Held.Set(leftOver == 0 ? ItemStack.Empty : heldStack);
            }
            else
            {
                int leftOver = inventory.AddItem(heldStack.ItemId, heldStack.Count, maxStack);

                if (leftOver == 0)
                {
                    Held.Clear();
                }
                else
                {
                    ItemStack partial = heldStack;
                    partial.Count = leftOver;
                    Held.Set(partial);
                }
            }
        }

        /// <summary>
        ///     Resets paint/hover state. Call on screen close.
        /// </summary>
        public void ResetState()
        {
            _isPainting = false;
            _paintPending = false;
            _paintedSlots.Clear();
            HoveredContainer = null;
            HoveredSlotIndex = -1;
        }

        // ────────────────────────────────────────────────────────────────────
        // Private helpers
        // ────────────────────────────────────────────────────────────────────

        private void StartPaint(ISlotContainer container, int slotIndex)
        {
            _isPainting = false;
            _paintPending = true;
            _paintOriginSlot = slotIndex;
            _paintOriginContainer = container;
            _paintItemId = Held.IsEmpty ? default : Held.Stack.ItemId;
            _paintDurability = Held.IsEmpty ? -1 : Held.Stack.Durability;
            _paintComponents = Held.IsEmpty ? null : Held.Stack.Components;
            _paintedSlots.Clear();
            _paintedSlots.Add(MakePaintKey(container, slotIndex));
        }

        private void HandlePaintHover(ISlotContainer container, int slotIndex)
        {
            // Activate pending paint when pointer enters a different slot than origin
            if (_paintPending && (container != _paintOriginContainer || slotIndex != _paintOriginSlot))
            {
                _isPainting = true;
                _paintPending = false;
            }

            if (!_isPainting || slotIndex < 0)
            {
                return;
            }

            if (container.IsReadOnly)
            {
                return;
            }

            long key = MakePaintKey(container, slotIndex);

            if (_paintedSlots.Contains(key))
            {
                return;
            }

            if (Held.IsEmpty)
            {
                _isPainting = false;
                _paintPending = false;
                _paintedSlots.Clear();
                return;
            }

            ItemStack slotItem = container.GetSlot(slotIndex);

            bool canPlace = false;

            if (slotItem.IsEmpty)
            {
                canPlace = true;
            }
            else if (ItemStack.CanStack(slotItem, BuildPaintProbe()))
            {
                int max = GetMaxStack(slotItem);
                canPlace = slotItem.Count < max;
            }

            if (!canPlace)
            {
                return;
            }

            // Place 1
            if (slotItem.IsEmpty)
            {
                ItemStack paintStack = new(_paintItemId, 1, _paintDurability);
                paintStack.Components = _paintComponents;
                container.SetSlot(slotIndex, paintStack);
            }
            else
            {
                ItemStack updated = slotItem;
                updated.Count += 1;
                container.SetSlot(slotIndex, updated);
            }

            ItemStack h = Held.Stack;
            h.Count -= 1;
            Held.Set(h.IsEmpty ? ItemStack.Empty : h);
            _paintedSlots.Add(key);
            container.OnSlotChanged(slotIndex);
        }

        private int GetMaxStack(ItemStack stack)
        {
            if (stack.IsEmpty)
            {
                return 64;
            }

            ItemEntry def = _itemRegistry.Get(stack.ItemId);
            return def != null ? def.MaxStackSize : 64;
        }

        private int TransferToContainer(ItemStack source, ISlotContainer target, int maxStack)
        {
            int remaining = source.Count;

            // First pass: fill existing stacks of the same item
            for (int i = 0; i < target.SlotCount && remaining > 0; i++)
            {
                ItemStack slot = target.GetSlot(i);

                if (slot.IsEmpty || !ItemStack.CanStack(slot, source))
                {
                    continue;
                }

                int space = maxStack - slot.Count;

                if (space <= 0)
                {
                    continue;
                }

                int toMove = Mathf.Min(remaining, space);
                ItemStack updated = slot;
                updated.Count += toMove;
                target.SetSlot(i, updated);
                remaining -= toMove;
            }

            // Second pass: empty slots
            for (int i = 0; i < target.SlotCount && remaining > 0; i++)
            {
                if (!target.GetSlot(i).IsEmpty)
                {
                    continue;
                }

                int toMove = Mathf.Min(remaining, maxStack);
                ItemStack newSlot = new(source.ItemId, toMove, source.Durability);
                newSlot.Components = source.Components;
                target.SetSlot(i, newSlot);
                remaining -= toMove;
            }

            return remaining;
        }

        private ItemStack BuildPaintProbe()
        {
            ItemStack probe = new(_paintItemId, 1, _paintDurability);
            probe.Components = _paintComponents;
            return probe;
        }

        private static long MakePaintKey(ISlotContainer container, int slotIndex)
        {
            return (long)container.GetHashCode() << 32 | (uint)slotIndex;
        }
    }
}
