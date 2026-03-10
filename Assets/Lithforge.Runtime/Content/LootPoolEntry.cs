using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [System.Serializable]
    public sealed class LootPoolEntry
    {
        [Tooltip("Minimum rolls")]
        [Min(0)]
        [SerializeField] private int rollsMin = 1;

        [Tooltip("Maximum rolls")]
        [Min(0)]
        [SerializeField] private int rollsMax = 1;

        [Tooltip("Entries in this pool")]
        [SerializeField] private List<LootItemEntry> entries = new List<LootItemEntry>();

        [Tooltip("Conditions for this pool")]
        [SerializeField] private List<LootConditionEntry> conditions = new List<LootConditionEntry>();

        public int RollsMin
        {
            get { return rollsMin; }
        }

        public int RollsMax
        {
            get { return rollsMax; }
        }

        public IReadOnlyList<LootItemEntry> Entries
        {
            get { return entries; }
        }

        public IReadOnlyList<LootConditionEntry> Conditions
        {
            get { return conditions; }
        }
    }
}
