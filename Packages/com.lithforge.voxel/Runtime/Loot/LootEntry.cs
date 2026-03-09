using System.Collections.Generic;

namespace Lithforge.Voxel.Loot
{
    /// <summary>
    /// A single entry in a loot pool.
    /// Can be an item drop, an empty result, or a reference to another loot table.
    /// </summary>
    public sealed class LootEntry
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public int Weight { get; set; }
        public List<LootCondition> Conditions { get; set; }
        public List<LootFunction> Functions { get; set; }

        public LootEntry()
        {
            Type = "item";
            Name = "";
            Weight = 1;
            Conditions = new List<LootCondition>();
            Functions = new List<LootFunction>();
        }
    }
}
