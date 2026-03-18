using System.Collections.Generic;

namespace Lithforge.Item.Loot
{
    /// <summary>
    /// A pool of loot entries. Each pool is rolled independently.
    /// The number of rolls determines how many entries are selected.
    /// </summary>
    public sealed class LootPool
    {
        public int RollsMin { get; set; } = 1;
        public int RollsMax { get; set; } = 1;
        public List<LootEntry> Entries { get; set; } = new();
        public List<LootCondition> Conditions { get; set; } = new();
    }
}
