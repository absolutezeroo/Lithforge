using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Loot
{
    [System.Serializable]
    public sealed class LootPoolEntry
    {
        [FormerlySerializedAs("rollsMin")]
        [Tooltip("Minimum rolls")]
        [Min(0)]
        [SerializeField] private int _rollsMin = 1;

        [FormerlySerializedAs("rollsMax")]
        [Tooltip("Maximum rolls")]
        [Min(0)]
        [SerializeField] private int _rollsMax = 1;

        [FormerlySerializedAs("entries")]
        [Tooltip("Entries in this pool")]
        [SerializeField] private List<LootItemEntry> _entries = new List<LootItemEntry>();

        [FormerlySerializedAs("conditions")]
        [Tooltip("Conditions for this pool")]
        [SerializeField] private List<LootConditionEntry> _conditions = new List<LootConditionEntry>();

        public int RollsMin
        {
            get { return _rollsMin; }
        }

        public int RollsMax
        {
            get { return _rollsMax; }
        }

        public IReadOnlyList<LootItemEntry> Entries
        {
            get { return _entries; }
        }

        public IReadOnlyList<LootConditionEntry> Conditions
        {
            get { return _conditions; }
        }
    }
}
