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
    /// Screen-agnostic controller for all slot interaction modes:
    ///   1. Left-click:   pick up / place / merge / swap
    ///   2. Right-click:  pick up half / place 1
    ///   3. Shift+click:  quick transfer between hotbar and main
    ///   4. Paint-drag:   right-click drag to distribute 1 item per slot
    ///   5. Number keys:  swap hovered slot with hotbar slot
    ///   6. Output take:  consume craft ingredients and collect result
    /// Operates entirely on ISlotContainer — no knowledge of specific screen layout.
    /// </summary>
    public sealed class SlotInteractionController
    {
        private readonly HeldStack _held;
        private readonly ItemRegistry _itemRegistry;

        // Paint mode state
        private bool _isPainting;
        private bool _paintPending;
        private int _paintOriginSlot;
        private ISlotContainer _paintOriginContainer;
        private ResourceId _paintItemId;
        private int _paintDurability;
        private byte[] _paintCustomData;
        private readonly HashSet<long> _paintedSlots = new HashSet<long>();

        // Hover tracking
        private ISlotContainer _hoveredContainer;
        private int _hoveredSlotIndex = -1;

        public SlotInteractionController(HeldStack held, ItemRegistry itemRegistry)
        {
            _held = held;
            _itemRegistry = itemRegistry;
        }

        public HeldStack Held
        {
            get { return _held; }
        }

        public int HoveredSlotIndex
        {
            get { return _hoveredSlotIndex; }
        }

        public ISlotContainer HoveredContainer
        {
            get { return _hoveredContainer; }
        }

        // ────────────────────────────────────────────────────────────────────
        // Hover tracking
        // ────────────────────────────────────────────────────────────────────

        public void OnSlotEnter(ISlotContainer container, int slotIndex)
        {
            _hoveredContainer = container;
            _hoveredSlotIndex = slotIndex;
            HandlePaintHover(container, slotIndex);
        }

        public void OnSlotLeave(ISlotContainer container, int slotIndex)
        {
            if (_hoveredContainer == container && _hoveredSlotIndex == slotIndex)
            {
                _hoveredContainer = null;
                _hoveredSlotIndex = -1;
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

            if (_held.IsEmpty && slotItem.IsEmpty)
            {
                return;
            }

            if (_held.IsEmpty)
            {
                // Pick up entire stack
                _held.Set(slotItem);
                container.SetSlot(slotIndex, ItemStack.Empty);
            }
            else if (slotItem.IsEmpty)
            {
                // Place entire held stack
                container.SetSlot(slotIndex, _held.Stack);
                _held.Clear();
            }
            else if (_held.Stack.ItemId == slotItem.ItemId)
            {
                // Merge held into slot
                int space = slotMaxStack - slotItem.Count;

                if (space <= 0)
                {
                    // Slot full, swap
                    container.SetSlot(slotIndex, _held.Stack);
                    _held.Set(slotItem);
                }
                else
                {
                    int toMove = Mathf.Min(_held.Stack.Count, space);
                    ItemStack newSlot = slotItem;
                    newSlot.Count += toMove;
                    container.SetSlot(slotIndex, newSlot);

                    ItemStack newHeld = _held.Stack;
                    newHeld.Count -= toMove;
                    _held.Set(newHeld.IsEmpty ? ItemStack.Empty : newHeld);
                }
            }
            else
            {
                // Swap different items
                container.SetSlot(slotIndex, _held.Stack);
                _held.Set(slotItem);
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

            if (_held.IsEmpty && slotItem.IsEmpty)
            {
                return;
            }

            if (_held.IsEmpty)
            {
                // Pick up half (rounded up)
                int half = (slotItem.Count + 1) / 2;
                ItemStack pickup = new ItemStack(slotItem.ItemId, half, slotItem.Durability);
                pickup.CustomData = slotItem.CustomData;
                _held.Set(pickup);

                ItemStack remaining = slotItem;
                remaining.Count -= half;
                container.SetSlot(slotIndex, remaining.IsEmpty ? ItemStack.Empty : remaining);
            }
            else if (slotItem.IsEmpty)
            {
                // Place 1
                ItemStack placed = new ItemStack(_held.Stack.ItemId, 1, _held.Stack.Durability);
                placed.CustomData = _held.Stack.CustomData;
                container.SetSlot(slotIndex, placed);

                ItemStack newHeld = _held.Stack;
                newHeld.Count -= 1;
                _held.Set(newHeld.IsEmpty ? ItemStack.Empty : newHeld);

                if (!_held.IsEmpty)
                {
                    StartPaint(container, slotIndex);
                }
            }
            else if (_held.Stack.ItemId == slotItem.ItemId && slotItem.Count < slotMaxStack)
            {
                // Place 1 on same item
                ItemStack newSlot = slotItem;
                newSlot.Count += 1;
                container.SetSlot(slotIndex, newSlot);

                ItemStack newHeld = _held.Stack;
                newHeld.Count -= 1;
                _held.Set(newHeld.IsEmpty ? ItemStack.Empty : newHeld);

                if (!_held.IsEmpty)
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
        /// Quick-transfers a slot between hotbar and main inventory adapters.
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
                ItemStack remainder = new ItemStack(slotItem.ItemId, remaining, slotItem.Durability);
                remainder.CustomData = slotItem.CustomData;
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
        /// Swaps the hovered inventory slot with the given hotbar slot index.
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
        /// Takes the crafting output. Adds result to held stack.
        /// </summary>
        public void TakeOutput(CraftingOutputContainerAdapter output, CraftingGridContainerAdapter grid)
        {
            if (output.CurrentMatch == null)
            {
                return;
            }

            ItemStack result = output.GetSlot(0);

            if (_held.IsEmpty)
            {
                _held.Set(result);
            }
            else if (_held.Stack.ItemId == result.ItemId)
            {
                int maxStack = GetMaxStack(_held.Stack);

                if (_held.Stack.Count + result.Count <= maxStack)
                {
                    ItemStack updated = _held.Stack;
                    updated.Count += result.Count;
                    _held.Set(updated);
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
        /// Shift+click on output: craft as many as possible and send to inventory.
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

                byte[] toolData = ToolTemplateRegistry.GetTemplate(match.ResultItem);

                if (toolData != null)
                {
                    ToolInstance tool = ToolInstanceSerializer.Deserialize(toolData);
                    int durability = tool != null ? tool.MaxDurability : -1;
                    ItemStack resultStack = new ItemStack(match.ResultItem, 1, durability);
                    resultStack.CustomData = toolData;
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
        /// Returns held items to inventory on screen close.
        /// </summary>
        public void ReturnHeldToInventory(Inventory inventory)
        {
            if (_held.IsEmpty)
            {
                return;
            }

            ItemStack heldStack = _held.Stack;
            ItemEntry def = _itemRegistry.Get(heldStack.ItemId);
            int maxStack = def != null ? def.MaxStackSize : 64;

            if (heldStack.HasCustomData)
            {
                int leftOver = inventory.AddItemStack(heldStack);
                _held.Set(leftOver == 0 ? ItemStack.Empty : heldStack);
            }
            else if (heldStack.Durability > 0)
            {
                int leftOver = inventory.AddItemWithDurability(heldStack.ItemId, heldStack.Durability);
                _held.Set(leftOver == 0 ? ItemStack.Empty : heldStack);
            }
            else
            {
                int leftOver = inventory.AddItem(heldStack.ItemId, heldStack.Count, maxStack);

                if (leftOver == 0)
                {
                    _held.Clear();
                }
                else
                {
                    ItemStack partial = heldStack;
                    partial.Count = leftOver;
                    _held.Set(partial);
                }
            }
        }

        /// <summary>
        /// Resets paint/hover state. Call on screen close.
        /// </summary>
        public void ResetState()
        {
            _isPainting = false;
            _paintPending = false;
            _paintedSlots.Clear();
            _hoveredContainer = null;
            _hoveredSlotIndex = -1;
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
            _paintItemId = _held.IsEmpty ? default : _held.Stack.ItemId;
            _paintDurability = _held.IsEmpty ? -1 : _held.Stack.Durability;
            _paintCustomData = _held.IsEmpty ? null : _held.Stack.CustomData;
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

            if (_held.IsEmpty)
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
            else if (slotItem.ItemId == _paintItemId)
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
                ItemStack paintStack = new ItemStack(_paintItemId, 1, _paintDurability);
                paintStack.CustomData = _paintCustomData;
                container.SetSlot(slotIndex, paintStack);
            }
            else
            {
                ItemStack updated = slotItem;
                updated.Count += 1;
                container.SetSlot(slotIndex, updated);
            }

            ItemStack h = _held.Stack;
            h.Count -= 1;
            _held.Set(h.IsEmpty ? ItemStack.Empty : h);
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

                if (slot.IsEmpty || slot.ItemId != source.ItemId)
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
                ItemStack newSlot = new ItemStack(source.ItemId, toMove, source.Durability);
                newSlot.CustomData = source.CustomData;
                target.SetSlot(i, newSlot);
                remaining -= toMove;
            }

            return remaining;
        }

        private static long MakePaintKey(ISlotContainer container, int slotIndex)
        {
            return ((long)container.GetHashCode() << 32) | (uint)slotIndex;
        }
    }
}
