using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Item
{
    /// <summary>
    /// Resolved item entry for the runtime item registry.
    /// Built from ScriptableObject data by the ContentPipeline.
    /// Tier 2 type — no UnityEngine dependencies.
    /// </summary>
    public sealed class ItemEntry
    {
        public ResourceId Id { get; }
        public int MaxStackSize { get; set; }
        public bool IsBlockItem { get; set; }
        public ResourceId BlockId { get; set; }
        public List<string> Tags { get; set; }

        /// <summary>
        /// Fuel burn time in seconds. 0 = not a fuel item.
        /// Used by FuelBurnBehavior in furnace block entities.
        /// </summary>
        public float FuelTime { get; set; }

        public ItemEntry(ResourceId id)
        {
            Id = id;
            MaxStackSize = 64;
            IsBlockItem = false;
            Tags = new List<string>();
        }
    }
}
