using Lithforge.Core.Data;

namespace Lithforge.Voxel.Crafting
{
    /// <summary>
    /// Runtime data for a material input entry.
    /// Maps an item to a material with a value/needed ratio.
    /// Equivalent to TiC's MaterialRecipe + IMaterialValue.
    /// </summary>
    public sealed class MaterialInputData
    {
        public ResourceId ItemId { get; }
        public ResourceId MaterialId { get; }
        public int Value { get; }
        public int Needed { get; }
        public ResourceId LeftoverItemId { get; }
        public bool HasLeftover { get; }

        public MaterialInputData(
            ResourceId itemId,
            ResourceId materialId,
            int value,
            int needed,
            ResourceId leftoverItemId)
        {
            ItemId = itemId;
            MaterialId = materialId;
            Value = value > 0 ? value : 1;
            Needed = needed > 0 ? needed : 1;
            LeftoverItemId = leftoverItemId;
            // default(ResourceId) has Namespace == null; only valid ResourceIds have non-null Namespace
            HasLeftover = leftoverItemId.Namespace != null;
        }

        /// <summary>
        /// Calculates how many input items are consumed for a part of the given cost.
        /// TiC formula: ceil(cost * needed / value).
        /// </summary>
        public int GetItemsUsed(int cost)
        {
            int total = cost * Needed;
            int used = total / Value;
            if (total % Value != 0)
            {
                used++;
            }
            return used;
        }

        /// <summary>
        /// Gets the number of leftover material units after crafting.
        /// TiC formula: (itemsUsed * value / needed) - cost, i.e. excess units.
        /// </summary>
        public int GetRemainderUnits(int cost)
        {
            int itemsUsed = GetItemsUsed(cost);
            int totalValue = itemsUsed * Value;
            int totalNeeded = cost * Needed;
            int remainder = totalValue - totalNeeded;
            return remainder;
        }

        /// <summary>
        /// Gets the leftover item count. Returns 0 if no leftover item defined
        /// or no remainder. Leftover items are assumed to have value=1 each
        /// (e.g. planks from a log), so remainder units map 1:1 to item count.
        /// </summary>
        public int GetLeftoverCount(int cost)
        {
            if (!HasLeftover)
            {
                return 0;
            }
            int remainder = GetRemainderUnits(cost);
            if (remainder <= 0)
            {
                return 0;
            }
            return remainder;
        }

        /// <summary>
        /// Gets the total material value from a stack of items.
        /// Returns a float for display (e.g. "2.5 units").
        /// </summary>
        public float GetMaterialValue(int itemCount)
        {
            return itemCount * Value / (float)Needed;
        }
    }
}
