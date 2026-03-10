using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [System.Serializable]
    public sealed class LootPoolEntry
    {
        [Tooltip("Minimum rolls")]
        [Min(0)]
        [SerializeField] private int _rollsMin = 1;

        [Tooltip("Maximum rolls")]
        [Min(0)]
        [SerializeField] private int _rollsMax = 1;

        [Tooltip("Entries in this pool")]
        [SerializeField] private List<LootItemEntry> _entries = new List<LootItemEntry>();

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
