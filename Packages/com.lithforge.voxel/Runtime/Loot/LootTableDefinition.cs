using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Voxel.Loot
{
    /// <summary>
    /// Data-driven loot table parsed from data/{ns}/loot_tables/{path}.json.
    /// Defines what items are dropped when a block is broken, an entity dies, etc.
    /// </summary>
    public sealed class LootTableDefinition
    {
        public ResourceId Id { get; }
        public string Type { get; set; }
        public List<LootPool> Pools { get; set; }

        public LootTableDefinition(ResourceId id)
        {
            Id = id;
            Type = "block";
            Pools = new List<LootPool>();
        }
    }
}
