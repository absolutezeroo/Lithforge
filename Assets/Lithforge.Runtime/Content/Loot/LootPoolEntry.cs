using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Loot
{
    [System.Serializable]
    public sealed class LootPoolEntry
    {
        [FormerlySerializedAs("_rollsMin"),Tooltip("Minimum rolls")]
        [Min(0)]
        [SerializeField] private int rollsMin = 1;

        [FormerlySerializedAs("_rollsMax"),Tooltip("Maximum rolls")]
        [Min(0)]
        [SerializeField] private int rollsMax = 1;

        [FormerlySerializedAs("_entries"),Tooltip("Entries in this pool")]
        [SerializeField] private List<LootItemEntry> entries = new List<LootItemEntry>();

        [FormerlySerializedAs("_conditions"),Tooltip("Conditions for this pool")]
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
