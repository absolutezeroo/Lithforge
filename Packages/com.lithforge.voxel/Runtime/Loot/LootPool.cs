using System.Collections.Generic;

namespace Lithforge.Voxel.Loot
{
    /// <summary>
    /// A pool of loot entries. Each pool is rolled independently.
    /// The number of rolls determines how many entries are selected.
    /// </summary>
    public sealed class LootPool
    {
        public int RollsMin { get; set; }
        public int RollsMax { get; set; }
        public List<LootEntry> Entries { get; set; }
        public List<LootCondition> Conditions { get; set; }

        public LootPool()
        {
            RollsMin = 1;
            RollsMax = 1;
            Entries = new List<LootEntry>();
            Conditions = new List<LootCondition>();
        }
    }
}
