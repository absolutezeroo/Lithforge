using System.IO;
using Lithforge.Core.Data;
using Lithforge.Voxel.Item;

namespace Lithforge.Runtime.BlockEntity.Behaviors
{
    /// <summary>
    /// Behavior that provides item storage slots to a block entity.
    /// Slot count is fixed at construction (27 for chest, 3 for furnace).
    /// Serializes items as resourceId string + count + durability.
    /// </summary>
    public sealed class InventoryBehavior : BlockEntityBehavior
    {
        private readonly ItemStack[] _slots;

        public int SlotCount
        {
            get { return _slots.Length; }
        }

        public InventoryBehavior(int slotCount)
        {
            _slots = new ItemStack[slotCount];
        }

        public ItemStack GetSlot(int index)
        {
            return _slots[index];
        }

        public void SetSlot(int index, ItemStack stack)
        {
            _slots[index] = stack;
        }

        /// <summary>
        /// Tries to add items to the first available slot with matching ID or empty slot.
        /// Returns the number of items that could not be added.
        /// </summary>
        public int AddItem(ResourceId itemId, int count, int maxStack)
        {
            int remaining = count;

            // First pass: merge into existing stacks
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (!_slots[i].IsEmpty && _slots[i].ItemId == itemId && _slots[i].Count < maxStack)
                {
                    int space = maxStack - _slots[i].Count;
                    int toAdd = remaining < space ? remaining : space;
                    ItemStack updated = _slots[i];
                    updated.Count += toAdd;
                    _slots[i] = updated;
                    remaining -= toAdd;
                }
            }

            // Second pass: fill empty slots
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    int toAdd = remaining < maxStack ? remaining : maxStack;
                    _slots[i] = new ItemStack(itemId, toAdd);
                    remaining -= toAdd;
                }
            }

            return remaining;
        }

        /// <summary>
        /// Returns true if any slot contains a non-empty item.
        /// </summary>
        public bool HasItems()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].IsEmpty)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Version sentinel: a large negative first int distinguishes versioned format
        /// from v1 (which writes a small positive slotCount as the first int).
        /// </summary>
        private const int VersionSentinel = int.MinValue + 2;

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(VersionSentinel);
            writer.Write(_slots.Length);

            for (int i = 0; i < _slots.Length; i++)
            {
                ItemStack slot = _slots[i];

                if (slot.IsEmpty)
                {
                    writer.Write(false);
                }
                else
                {
                    writer.Write(true);
                    writer.Write(slot.ItemId.ToString());
                    writer.Write(slot.Count);
                    writer.Write(slot.Durability);
                    writer.Write(slot.HasCustomData);

                    if (slot.HasCustomData)
                    {
                        writer.Write(slot.CustomData.Length);
                        writer.Write(slot.CustomData);
                    }
                }
            }
        }

        public override void Deserialize(BinaryReader reader)
        {
            int firstInt = reader.ReadInt32();
            bool hasCustomDataSupport;
            int count;

            if (firstInt == VersionSentinel)
            {
                hasCustomDataSupport = true;
                count = reader.ReadInt32();
            }
            else
            {
                hasCustomDataSupport = false;
                count = firstInt;
            }

            int readCount = count < _slots.Length ? count : _slots.Length;

            for (int i = 0; i < readCount; i++)
            {
                bool hasItem = reader.ReadBoolean();

                if (hasItem)
                {
                    string idStr = reader.ReadString();
                    int itemCount = reader.ReadInt32();
                    int durability = reader.ReadInt32();
                    ResourceId id = ResourceId.Parse(idStr);
                    ItemStack stack = new ItemStack(id, itemCount, durability);

                    if (hasCustomDataSupport)
                    {
                        bool hasCustomData = reader.ReadBoolean();

                        if (hasCustomData)
                        {
                            int dataLen = reader.ReadInt32();
                            stack.CustomData = reader.ReadBytes(dataLen);
                        }
                    }

                    _slots[i] = stack;
                }
                else
                {
                    _slots[i] = ItemStack.Empty;
                }
            }

            // Skip extra serialized slots if the stored count exceeds current capacity
            for (int i = readCount; i < count; i++)
            {
                bool hasItem = reader.ReadBoolean();

                if (hasItem)
                {
                    reader.ReadString();
                    reader.ReadInt32();
                    reader.ReadInt32();

                    if (hasCustomDataSupport)
                    {
                        bool hasCustomData = reader.ReadBoolean();

                        if (hasCustomData)
                        {
                            int dataLen = reader.ReadInt32();
                            reader.ReadBytes(dataLen);
                        }
                    }
                }
            }
        }
    }
}
