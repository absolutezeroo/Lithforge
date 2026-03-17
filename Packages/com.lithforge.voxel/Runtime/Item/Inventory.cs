using System;

using Lithforge.Core.Data;

namespace Lithforge.Voxel.Item
{
    /// <summary>
    ///     Player inventory with 36 slots (9 hotbar + 27 main).
    ///     Slots 0-8 are the hotbar, slots 9-35 are the main inventory.
    /// </summary>
    public sealed class Inventory
    {
        public const int SlotCount = 36;
        public const int HotbarSize = 9;

        private readonly ItemStack[] _slots = new ItemStack[SlotCount];
        private int _selectedSlot;

        /// <summary>
        ///     Monotonically increasing state ID. Incremented on every mutation.
        ///     Used for server-client sync: client sends its last-known stateId
        ///     with each SlotClickCommand. Server rejects if mismatch.
        /// </summary>
        public uint StateId { get; private set; }

        /// <summary>
        ///     Gets or sets the currently selected hotbar slot (0-8).
        /// </summary>
        public int SelectedSlot
        {
            get { return _selectedSlot; }
            set
            {
                if (value >= 0 && value < HotbarSize)
                {
                    _selectedSlot = value;
                }
            }
        }

        /// <summary>Increments state ID. Called after any slot mutation.</summary>
        public void IncrementStateId()
        {
            StateId++;
        }

        /// <summary>
        ///     Returns the ItemStack in the currently selected hotbar slot.
        /// </summary>
        public ItemStack GetSelectedItem()
        {
            return _slots[_selectedSlot];
        }

        /// <summary>
        ///     Returns the ItemStack at the given slot index.
        /// </summary>
        public ItemStack GetSlot(int index)
        {
            if (index < 0 || index >= SlotCount)
            {
                return ItemStack.Empty;
            }

            return _slots[index];
        }

        /// <summary>
        ///     Sets the ItemStack at the given slot index.
        /// </summary>
        public void SetSlot(int index, ItemStack stack)
        {
            if (index >= 0 && index < SlotCount)
            {
                _slots[index] = stack;
                IncrementStateId();
            }
        }

        /// <summary>
        ///     Adds items to the inventory, filling existing stacks first then empty slots.
        ///     Returns the number of items that could not be added (0 if all fit).
        /// </summary>
        public int AddItem(ResourceId itemId, int count, int maxStackSize)
        {
            int remaining = count;

            // First pass: fill existing stacks of the same item
            for (int i = 0; i < SlotCount && remaining > 0; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    continue;
                }

                if (_slots[i].ItemId != itemId)
                {
                    continue;
                }

                int space = maxStackSize - _slots[i].Count;

                if (space <= 0)
                {
                    continue;
                }

                int toAdd = remaining < space ? remaining : space;
                ItemStack updated = _slots[i];
                updated.Count += toAdd;
                _slots[i] = updated;
                remaining -= toAdd;
            }

            // Second pass: fill empty slots
            for (int i = 0; i < SlotCount && remaining > 0; i++)
            {
                if (!_slots[i].IsEmpty)
                {
                    continue;
                }

                int toAdd = remaining < maxStackSize ? remaining : maxStackSize;
                _slots[i] = new ItemStack(itemId, toAdd);
                remaining -= toAdd;
            }

            if (remaining < count)
            {
                IncrementStateId();
            }

            return remaining;
        }

        /// <summary>
        ///     Adds a single item with durability tracking (for tools).
        ///     Tools always occupy their own slot with count=1.
        ///     Returns the number of items that could not be added.
        /// </summary>
        public int AddItemWithDurability(ResourceId itemId, int durability)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    _slots[i] = new ItemStack(itemId, 1, durability);
                    IncrementStateId();

                    return 0;
                }
            }

            return 1;
        }

        /// <summary>
        ///     Adds a complete ItemStack preserving all fields (Durability, CustomData).
        ///     Used for tools and other items with custom runtime data.
        ///     Tools always occupy their own slot with count=1.
        ///     Returns 0 on success, 1 if no empty slot available.
        /// </summary>
        public int AddItemStack(ItemStack stack)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    _slots[i] = stack;
                    IncrementStateId();
                    return 0;
                }
            }

            return 1;
        }

        /// <summary>
        ///     Removes a number of items from a specific slot.
        ///     Returns the number actually removed.
        /// </summary>
        public int RemoveFromSlot(int index, int count)
        {
            if (index < 0 || index >= SlotCount || _slots[index].IsEmpty)
            {
                return 0;
            }

            int toRemove = count < _slots[index].Count ? count : _slots[index].Count;
            ItemStack updated = _slots[index];
            updated.Count -= toRemove;

            if (updated.Count <= 0)
            {
                _slots[index] = ItemStack.Empty;
            }
            else
            {
                _slots[index] = updated;
            }

            IncrementStateId();
            return toRemove;
        }

        /// <summary>
        ///     Finds the first slot containing the given item.
        ///     Returns -1 if not found.
        /// </summary>
        public int FindFirst(ResourceId itemId)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (!_slots[i].IsEmpty && _slots[i].ItemId == itemId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        ///     Returns true if the inventory has room for the given number of items.
        /// </summary>
        public bool CanAdd(ResourceId itemId, int count, int maxStackSize)
        {
            int remaining = count;

            for (int i = 0; i < SlotCount && remaining > 0; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    remaining -= maxStackSize;
                }
                else if (_slots[i].ItemId == itemId)
                {
                    int space = maxStackSize - _slots[i].Count;
                    remaining -= space;
                }
            }

            return remaining <= 0;
        }

        /// <summary>
        ///     Clears all inventory slots.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _slots[i] = ItemStack.Empty;
            }

            IncrementStateId();
        }

        /// <summary>
        ///     Returns a full copy of all slots for network sync.
        ///     Caller receives an array they own — no shared references.
        /// </summary>
        public ItemStack[] GetFullSnapshot()
        {
            ItemStack[] snapshot = new ItemStack[SlotCount];
            Array.Copy(_slots, snapshot, SlotCount);
            return snapshot;
        }

        /// <summary>
        ///     Overwrites all slots from a server snapshot. Used for full resync
        ///     after state ID mismatch.
        /// </summary>
        public void ApplyFullSnapshot(ItemStack[] snapshot, uint newStateId)
        {
            Array.Copy(snapshot, _slots, SlotCount);
            StateId = newStateId;
        }
    }
}
