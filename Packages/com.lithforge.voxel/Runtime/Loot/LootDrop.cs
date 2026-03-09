using Lithforge.Core.Data;

namespace Lithforge.Voxel.Loot
{
    /// <summary>
    /// Represents a concrete item drop resolved from a loot table.
    /// </summary>
    public readonly struct LootDrop
    {
        public ResourceId ItemId { get; }
        public int Count { get; }

        public LootDrop(ResourceId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
}
