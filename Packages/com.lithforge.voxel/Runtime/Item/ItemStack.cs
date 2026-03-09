using Lithforge.Core.Data;

namespace Lithforge.Voxel.Item
{
    /// <summary>
    /// Represents a stack of items in an inventory slot.
    /// Value type — default is empty (count 0).
    /// </summary>
    public struct ItemStack
    {
        public ResourceId ItemId { get; set; }

        public int Count { get; set; }

        public int Durability { get; set; }

        public static readonly ItemStack Empty = default;

        public ItemStack(ResourceId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
            Durability = -1;
        }

        public ItemStack(ResourceId itemId, int count, int durability)
        {
            ItemId = itemId;
            Count = count;
            Durability = durability;
        }

        public bool IsEmpty
        {
            get { return Count <= 0; }
        }
    }
}
