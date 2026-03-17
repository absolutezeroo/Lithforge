using System.Collections.Generic;

namespace Lithforge.Voxel.Loot
{
    /// <summary>
    /// A single entry in a loot pool.
    /// Can be an item drop, an empty result, or a reference to another loot table.
    /// </summary>
    public sealed class LootEntry
    {
        public string Type { get; set; } = "item";
        public string Name { get; set; } = "";
        public int Weight { get; set; } = 1;
        public List<LootCondition> Conditions { get; set; } = new();
        public List<LootFunction> Functions { get; set; } = new();
    }
}
