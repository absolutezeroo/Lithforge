using System.IO;

using Lithforge.Core.Data;
using Lithforge.Item;

namespace Lithforge.Runtime.BlockEntity.Behaviors
{
    /// <summary>
    ///     Behavior that provides item storage slots to a block entity.
    ///     Slot count is fixed at construction (27 for chest, 3 for furnace).
    ///     Serializes items as resourceId string + count + durability + components.
    /// </summary>
    public sealed class InventoryBehavior : BlockEntityBehavior
    {
        /// <summary>
        ///     v1: small positive slotCount as first int (no sentinel).
        ///     v2: sentinel int.MinValue + 2, supports byte[] CustomData.
        ///     v3: sentinel int.MinValue + 3, supports typed DataComponentMap.
        /// </summary>
        /// <summary>Sentinel value indicating v2 serialization format with byte[] CustomData.</summary>
        private const int V2Sentinel = int.MinValue + 2;

        /// <summary>Sentinel value indicating v3 serialization format with typed DataComponentMap.</summary>
        private const int V3Sentinel = int.MinValue + 3;

        /// <summary>Fixed-size array of item slots.</summary>
        private readonly ItemStack[] _slots;

        /// <summary>Creates an inventory behavior with the specified number of slots.</summary>
        public InventoryBehavior(int slotCount)
        {
            _slots = new ItemStack[slotCount];
        }

        /// <summary>Total number of item slots in this inventory.</summary>
        public int SlotCount
        {
            get { return _slots.Length; }
        }

        /// <summary>Returns the item stack at the given slot index.</summary>
        public ItemStack GetSlot(int index)
        {
            return _slots[index];
        }

        /// <summary>Sets the item stack at the given slot index.</summary>
        public void SetSlot(int index, ItemStack stack)
        {
            _slots[index] = stack;
        }

        /// <summary>
        ///     Tries to add items to the first available slot with matching ID or empty slot.
        ///     Returns the number of items that could not be added.
        /// </summary>
        public int AddItem(ResourceId itemId, int count, int maxStack)
        {
            int remaining = count;

            // First pass: merge into existing stacks (only plain items)
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (!_slots[i].IsEmpty && _slots[i].ItemId == itemId
                                       && !_slots[i].HasComponents && _slots[i].Count < maxStack)
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
        ///     Returns true if any slot contains a non-empty item.
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

        /// <summary>Serializes all slots to the writer using v3 format with DataComponentMap support.</summary>
        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(V3Sentinel);
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
                    DataComponentRegistry.Serialize(slot.Components, writer);
                }
            }
        }

        /// <summary>Deserializes slots from the reader, auto-detecting v1/v2/v3 format.</summary>
        public override void Deserialize(BinaryReader reader)
        {
            int firstInt = reader.ReadInt32();
            int formatVersion;
            int count;

            if (firstInt == V3Sentinel)
            {
                formatVersion = 3;
                count = reader.ReadInt32();
            }
            else if (firstInt == V2Sentinel)
            {
                formatVersion = 2;
                count = reader.ReadInt32();
            }
            else
            {
                formatVersion = 1;
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
                    ItemStack stack = new(id, itemCount, durability);

                    if (formatVersion == 3)
                    {
                        stack.Components = DataComponentRegistry.Deserialize(reader);
                    }
                    else if (formatVersion == 2)
                    {
                        bool hasCustomData = reader.ReadBoolean();

                        if (hasCustomData)
                        {
                            int dataLen = reader.ReadInt32();
                            byte[] customData = reader.ReadBytes(dataLen);
                            stack.Components = LegacyCustomDataMigrator.Migrate(customData);
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

                    if (formatVersion == 3)
                    {
                        DataComponentRegistry.Deserialize(reader);
                    }
                    else if (formatVersion == 2)
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
