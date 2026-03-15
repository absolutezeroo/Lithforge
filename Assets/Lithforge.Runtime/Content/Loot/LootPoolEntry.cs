using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Loot
{
    /// <summary>
    /// A pool within a <see cref="LootTable"/>. Each pool is rolled independently:
    /// a random number of rolls between min and max, each selecting one entry by weighted random.
    /// </summary>
    [System.Serializable]
    public sealed class LootPoolEntry
    {
        /// <summary>Minimum number of times this pool is rolled (inclusive).</summary>
        [FormerlySerializedAs("_rollsMin"),Tooltip("Minimum rolls")]
        [Min(0)]
        [SerializeField] private int rollsMin = 1;

        /// <summary>Maximum number of times this pool is rolled (inclusive).</summary>
        [FormerlySerializedAs("_rollsMax"),Tooltip("Maximum rolls")]
        [Min(0)]
        [SerializeField] private int rollsMax = 1;

        /// <summary>Weighted-random entries — one is selected per roll.</summary>
        [FormerlySerializedAs("_entries"),Tooltip("Entries in this pool")]
        [SerializeField] private List<LootItemEntry> entries = new List<LootItemEntry>();

        /// <summary>All conditions must pass for this pool to be rolled at all.</summary>
        [FormerlySerializedAs("_conditions"),Tooltip("Conditions for this pool")]
        [SerializeField] private List<LootConditionEntry> conditions = new List<LootConditionEntry>();

        /// <summary>Minimum number of times this pool is rolled (inclusive).</summary>
        public int RollsMin
        {
            get { return rollsMin; }
        }

        /// <summary>Maximum number of times this pool is rolled (inclusive). Actual rolls are random in [min, max].</summary>
        public int RollsMax
        {
            get { return rollsMax; }
        }

        /// <summary>Weighted-random entries — one is selected per roll.</summary>
        public IReadOnlyList<LootItemEntry> Entries
        {
            get { return entries; }
        }

        /// <summary>All conditions must pass for this pool to be rolled at all.</summary>
        public IReadOnlyList<LootConditionEntry> Conditions
        {
            get { return conditions; }
        }
    }
}
