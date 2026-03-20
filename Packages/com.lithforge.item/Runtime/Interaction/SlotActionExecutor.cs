using System;

using Lithforge.Core.Data;

namespace Lithforge.Item.Interaction
{
    /// <summary>
    ///     Pure Tier 2 slot mutation executor. Implements the authoritative logic for
    ///     inventory slot clicks, shared by both client prediction and server re-execution.
    ///     SHARED LOGIC — any change to click rules here MUST be reflected in
    ///     SlotInteractionController (Tier 3). See CLAUDE.md Known Code Duplication table.
    /// </summary>
    public static class SlotActionExecutor
    {
        /// <summary>Click type: left-click (pick up / place / merge / swap).</summary>
        public const byte ClickLeft = 0;

        /// <summary>Click type: right-click (pick up half / place 1).</summary>
        public const byte ClickRight = 1;

        /// <summary>Click type: shift+left-click (quick transfer).</summary>
        public const byte ClickShiftLeft = 2;

        /// <summary>Click type: paint-drag (distribute 1 per slot).</summary>
        public const byte ClickPaintDrag = 3;

        /// <summary>Click type: number key swap.</summary>
        public const byte ClickNumberKey = 4;

        /// <summary>Click type: crafting output take (trusted, not re-executed).</summary>
        public const byte ClickOutputTake = 5;

        /// <summary>
        ///     Executes a slot click on the given inventory and cursor.
        ///     Returns which slots changed and whether the cursor changed.
        ///     For click types 0-2, 4: performs anti-dupe conservation check.
        ///     For click type 5 (crafting output): trusted pass-through, no re-execution.
        ///     Click type 3 (paint-drag) is handled per-slot by ExecutePaintSlot.
        /// </summary>
        public static SlotActionResult Execute(
            Inventory inventory,
            ref ItemStack cursor,
            int slotIndex,
            byte clickType,
            byte button,
            ItemRegistry itemRegistry)
        {
            if (clickType == ClickOutputTake)
            {
                return SlotActionResult.NoOp();
            }

            if (clickType == ClickPaintDrag)
            {
                return ExecutePaintSlot(inventory, ref cursor, slotIndex, itemRegistry);
            }

            if (slotIndex < 0 || slotIndex >= Inventory.SlotCount)
            {
                return SlotActionResult.Fail(SlotActionOutcome.InvalidSlot);
            }

            // Anti-dupe: count total items before execution
            int totalBefore = CountTotalItems(inventory, ref cursor);

            SlotActionResult result = new() { Outcome = SlotActionOutcome.Success };

            switch (clickType)
            {
                case ClickLeft:
                    ExecuteLeftClick(inventory, ref cursor, slotIndex, itemRegistry, ref result);
                    break;
                case ClickRight:
                    ExecuteRightClick(inventory, ref cursor, slotIndex, itemRegistry, ref result);
                    break;
                case ClickShiftLeft:
                    ExecuteShiftClick(inventory, slotIndex, itemRegistry, ref result);
                    break;
                case ClickNumberKey:
                    ExecuteNumberKeySwap(inventory, slotIndex, button, ref result);
                    break;
                default:
                    return SlotActionResult.Fail(SlotActionOutcome.InvalidAction);
            }

            // Anti-dupe: verify total items are conserved
            int totalAfter = CountTotalItems(inventory, ref cursor);

            if (totalAfter != totalBefore)
            {
                // Conservation violation — this should never happen with correct logic.
                // In a future iteration, we could roll back by restoring from a pre-snapshot.
                return SlotActionResult.Fail(SlotActionOutcome.ItemCountMismatch);
            }

            return result;
        }

        /// <summary>Executes a single paint-drag slot placement. Places 1 item from cursor into slot.</summary>
        public static SlotActionResult ExecutePaintSlot(
            Inventory inventory,
            ref ItemStack cursor,
            int slotIndex,
            ItemRegistry itemRegistry)
        {
            if (cursor.IsEmpty || slotIndex < 0 || slotIndex >= Inventory.SlotCount)
            {
                return SlotActionResult.NoOp();
            }

            ItemStack slotItem = inventory.GetSlot(slotIndex);
            int maxStack = GetMaxStack(slotItem.IsEmpty ? cursor : slotItem, itemRegistry);

            bool canPlace = false;

            if (slotItem.IsEmpty)
            {
                canPlace = true;
            }
            else if (ItemStack.CanStack(slotItem, cursor) && slotItem.Count < maxStack)
            {
                canPlace = true;
            }

            if (!canPlace)
            {
                return SlotActionResult.NoOp();
            }

            int totalBefore = CountTotalItems(inventory, ref cursor);

            if (slotItem.IsEmpty)
            {
                ItemStack placed = new(cursor.ItemId, 1, cursor.Durability)
                {
                    Components = cursor.Components,
                };
                inventory.SetSlot(slotIndex, placed);
            }
            else
            {
                ItemStack updated = slotItem;
                updated.Count += 1;
                inventory.SetSlot(slotIndex, updated);
            }

            ItemStack newCursor = cursor;
            newCursor.Count -= 1;
            cursor = newCursor.IsEmpty ? ItemStack.Empty : newCursor;

            int totalAfter = CountTotalItems(inventory, ref cursor);

            if (totalAfter != totalBefore)
            {
                return SlotActionResult.Fail(SlotActionOutcome.ItemCountMismatch);
            }

            SlotActionResult result = new() { Outcome = SlotActionOutcome.Success, CursorChanged = true };
            result.AddChangedSlot(slotIndex);
            return result;
        }

        /// <summary>Left-click: pick up, place, merge, or swap items between cursor and slot.</summary>
        private static void ExecuteLeftClick(
            Inventory inventory,
            ref ItemStack cursor,
            int slotIndex,
            ItemRegistry itemRegistry,
            ref SlotActionResult result)
        {
            ItemStack slotItem = inventory.GetSlot(slotIndex);
            int slotMaxStack = GetMaxStack(slotItem.IsEmpty ? cursor : slotItem, itemRegistry);

            if (cursor.IsEmpty && slotItem.IsEmpty)
            {
                return;
            }

            if (cursor.IsEmpty)
            {
                // Pick up entire stack
                cursor = slotItem;
                inventory.SetSlot(slotIndex, ItemStack.Empty);
                result.CursorChanged = true;
                result.AddChangedSlot(slotIndex);
            }
            else if (slotItem.IsEmpty)
            {
                // Place entire held stack
                inventory.SetSlot(slotIndex, cursor);
                cursor = ItemStack.Empty;
                result.CursorChanged = true;
                result.AddChangedSlot(slotIndex);
            }
            else if (ItemStack.CanStack(cursor, slotItem))
            {
                // Merge cursor into slot
                int space = slotMaxStack - slotItem.Count;

                if (space <= 0)
                {
                    // Slot full, swap
                    inventory.SetSlot(slotIndex, cursor);
                    cursor = slotItem;
                    result.CursorChanged = true;
                    result.AddChangedSlot(slotIndex);
                }
                else
                {
                    int toMove = Math.Min(cursor.Count, space);
                    ItemStack newSlot = slotItem;
                    newSlot.Count += toMove;
                    inventory.SetSlot(slotIndex, newSlot);

                    ItemStack newCursor = cursor;
                    newCursor.Count -= toMove;
                    cursor = newCursor.IsEmpty ? ItemStack.Empty : newCursor;
                    result.CursorChanged = true;
                    result.AddChangedSlot(slotIndex);
                }
            }
            else
            {
                // Swap different items
                inventory.SetSlot(slotIndex, cursor);
                cursor = slotItem;
                result.CursorChanged = true;
                result.AddChangedSlot(slotIndex);
            }
        }

        /// <summary>Right-click: pick up half, place 1, or no-op for different items.</summary>
        private static void ExecuteRightClick(
            Inventory inventory,
            ref ItemStack cursor,
            int slotIndex,
            ItemRegistry itemRegistry,
            ref SlotActionResult result)
        {
            ItemStack slotItem = inventory.GetSlot(slotIndex);
            int slotMaxStack = GetMaxStack(slotItem.IsEmpty ? cursor : slotItem, itemRegistry);

            if (cursor.IsEmpty && slotItem.IsEmpty)
            {
                return;
            }

            if (cursor.IsEmpty)
            {
                // Pick up half (rounded up)
                int half = (slotItem.Count + 1) / 2;
                ItemStack pickup = new(slotItem.ItemId, half, slotItem.Durability)
                {
                    Components = slotItem.Components,
                };
                cursor = pickup;

                ItemStack remaining = slotItem;
                remaining.Count -= half;
                inventory.SetSlot(slotIndex, remaining.IsEmpty ? ItemStack.Empty : remaining);
                result.CursorChanged = true;
                result.AddChangedSlot(slotIndex);
            }
            else if (slotItem.IsEmpty)
            {
                // Place 1
                ItemStack placed = new(cursor.ItemId, 1, cursor.Durability)
                {
                    Components = cursor.Components,
                };
                inventory.SetSlot(slotIndex, placed);

                ItemStack newCursor = cursor;
                newCursor.Count -= 1;
                cursor = newCursor.IsEmpty ? ItemStack.Empty : newCursor;
                result.CursorChanged = true;
                result.AddChangedSlot(slotIndex);
            }
            else if (ItemStack.CanStack(cursor, slotItem) && slotItem.Count < slotMaxStack)
            {
                // Place 1 on same item
                ItemStack newSlot = slotItem;
                newSlot.Count += 1;
                inventory.SetSlot(slotIndex, newSlot);

                ItemStack newCursor = cursor;
                newCursor.Count -= 1;
                cursor = newCursor.IsEmpty ? ItemStack.Empty : newCursor;
                result.CursorChanged = true;
                result.AddChangedSlot(slotIndex);
            }

            // Right-click on different item = nothing
        }

        /// <summary>Shift+left-click: quick transfer from slot to opposite inventory section.</summary>
        private static void ExecuteShiftClick(
            Inventory inventory,
            int slotIndex,
            ItemRegistry itemRegistry,
            ref SlotActionResult result)
        {
            ItemStack slotItem = inventory.GetSlot(slotIndex);

            if (slotItem.IsEmpty)
            {
                return;
            }

            int maxStack = GetMaxStack(slotItem, itemRegistry);

            // Determine target range: if source is hotbar (0-8), target is main (9-35); otherwise hotbar
            int targetStart;
            int targetEnd;

            if (slotIndex < Inventory.HotbarSize)
            {
                targetStart = Inventory.HotbarSize;
                targetEnd = Inventory.SlotCount;
            }
            else
            {
                targetStart = 0;
                targetEnd = Inventory.HotbarSize;
            }

            int remaining = slotItem.Count;

            // First pass: fill existing stacks of same item in target range
            for (int i = targetStart; i < targetEnd && remaining > 0; i++)
            {
                ItemStack target = inventory.GetSlot(i);

                if (target.IsEmpty || !ItemStack.CanStack(target, slotItem))
                {
                    continue;
                }

                int space = maxStack - target.Count;

                if (space <= 0)
                {
                    continue;
                }

                int toMove = Math.Min(remaining, space);
                ItemStack updated = target;
                updated.Count += toMove;
                inventory.SetSlot(i, updated);
                remaining -= toMove;
                result.AddChangedSlot(i);
            }

            // Second pass: fill empty slots in target range
            for (int i = targetStart; i < targetEnd && remaining > 0; i++)
            {
                if (!inventory.GetSlot(i).IsEmpty)
                {
                    continue;
                }

                int toMove = Math.Min(remaining, maxStack);
                ItemStack newSlot = new(slotItem.ItemId, toMove, slotItem.Durability)
                {
                    Components = slotItem.Components,
                };
                inventory.SetSlot(i, newSlot);
                remaining -= toMove;
                result.AddChangedSlot(i);
            }

            // Update source slot
            if (remaining == 0)
            {
                inventory.SetSlot(slotIndex, ItemStack.Empty);
            }
            else
            {
                ItemStack updated = slotItem;
                updated.Count = remaining;
                inventory.SetSlot(slotIndex, updated);
            }

            result.AddChangedSlot(slotIndex);
        }

        /// <summary>Number key swap: swaps hovered slot with the specified hotbar slot.</summary>
        private static void ExecuteNumberKeySwap(
            Inventory inventory,
            int slotIndex,
            byte hotbarSlot,
            ref SlotActionResult result)
        {
            if (hotbarSlot >= Inventory.HotbarSize)
            {
                return;
            }

            // Don't swap with self
            if (slotIndex == hotbarSlot)
            {
                return;
            }

            ItemStack hotbarItem = inventory.GetSlot(hotbarSlot);
            ItemStack hoveredItem = inventory.GetSlot(slotIndex);

            inventory.SetSlot(hotbarSlot, hoveredItem);
            inventory.SetSlot(slotIndex, hotbarItem);
            result.AddChangedSlot(hotbarSlot);
            result.AddChangedSlot(slotIndex);
        }

        /// <summary>Returns the max stack size for an item, defaulting to 64 if unknown.</summary>
        private static int GetMaxStack(ItemStack stack, ItemRegistry itemRegistry)
        {
            if (stack.IsEmpty)
            {
                return 64;
            }

            ItemEntry def = itemRegistry.Get(stack.ItemId);
            return def?.MaxStackSize ?? 64;
        }

        /// <summary>
        ///     Counts total items across all inventory slots plus the cursor.
        ///     Used for anti-dupe conservation checks.
        /// </summary>
        private static int CountTotalItems(Inventory inventory, ref ItemStack cursor)
        {
            int total = 0;

            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                ItemStack slot = inventory.GetSlot(i);

                if (!slot.IsEmpty)
                {
                    total += slot.Count;
                }
            }

            if (!cursor.IsEmpty)
            {
                total += cursor.Count;
            }

            return total;
        }
    }
}
