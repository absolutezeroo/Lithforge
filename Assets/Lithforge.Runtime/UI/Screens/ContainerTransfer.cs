using Lithforge.Core.Data;
using Lithforge.Runtime.UI.Container;
using Lithforge.Voxel.Item;

namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    /// Static utility for shift-click item transfer between containers.
    /// Extracted from the per-screen duplicated implementations to a single
    /// shared location. Pure logic — no Unity dependencies.
    /// </summary>
    public static class ContainerTransfer
    {
        /// <summary>
        /// Transfers an item stack from <paramref name="source"/> at <paramref name="slotIndex"/>
        /// into <paramref name="primaryTarget"/>, then <paramref name="secondaryTarget"/> if any
        /// items remain. Updates the source slot with leftover count.
        /// </summary>
        public static void TransferItem(
            ISlotContainer source,
            int slotIndex,
            ISlotContainer primaryTarget,
            ISlotContainer secondaryTarget,
            ItemRegistry itemRegistry)
        {
            ItemStack stack = source.GetSlot(slotIndex);

            if (stack.IsEmpty)
            {
                return;
            }

            ItemEntry def = itemRegistry.Get(stack.ItemId);
            int maxStack = def != null ? def.MaxStackSize : 64;
            int remaining = stack.Count;

            remaining = TryFillContainer(stack, remaining, maxStack, primaryTarget);

            if (remaining > 0 && secondaryTarget != null)
            {
                remaining = TryFillContainer(stack, remaining, maxStack, secondaryTarget);
            }

            if (remaining == 0)
            {
                source.SetSlot(slotIndex, ItemStack.Empty);
            }
            else
            {
                ItemStack updated = stack;
                updated.Count = remaining;
                source.SetSlot(slotIndex, updated);
            }
        }

        /// <summary>
        /// Attempts to fill <paramref name="target"/> container slots with the given item.
        /// Merges into existing stacks first, then fills empty slots.
        /// Preserves Durability and Components from the source stack.
        /// Returns the count that could not be placed.
        /// </summary>
        public static int TryFillContainer(
            ItemStack source,
            int count,
            int maxStack,
            ISlotContainer target)
        {
            int remaining = count;
            ResourceId itemId = source.ItemId;

            // Merge into existing stacks first
            for (int i = 0; i < target.SlotCount && remaining > 0; i++)
            {
                ItemStack slot = target.GetSlot(i);

                if (!slot.IsEmpty && ItemStack.CanStack(slot, source) && slot.Count < maxStack)
                {
                    int space = maxStack - slot.Count;
                    int toAdd = remaining < space ? remaining : space;
                    ItemStack updated = slot;
                    updated.Count += toAdd;
                    target.SetSlot(i, updated);
                    remaining -= toAdd;
                }
            }

            // Fill empty slots
            for (int i = 0; i < target.SlotCount && remaining > 0; i++)
            {
                if (target.GetSlot(i).IsEmpty)
                {
                    int toAdd = remaining < maxStack ? remaining : maxStack;
                    ItemStack newSlot = new ItemStack(itemId, toAdd, source.Durability);
                    newSlot.Components = source.Components;
                    target.SetSlot(i, newSlot);
                    remaining -= toAdd;
                }
            }

            return remaining;
        }
    }
}
