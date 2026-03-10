using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [CreateAssetMenu(fileName = "NewLootTable", menuName = "Lithforge/Content/Loot Table", order = 6)]
    public sealed class LootTable : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Namespace for the resource id")]
        [SerializeField] private string _namespace = "lithforge";

        [Tooltip("Loot table name (e.g. 'blocks/stone')")]
        [SerializeField] private string _tableName = "";

        [Header("Type")]
        [Tooltip("Loot table type")]
        [SerializeField] private string _type = "block";

        [Header("Pools")]
        [Tooltip("Loot pools — each pool is rolled independently")]
        [SerializeField] private List<LootPoolEntry> _pools = new List<LootPoolEntry>();

        public string Namespace
        {
            get { return _namespace; }
        }

        public string TableName
        {
            get { return _tableName; }
        }

        public string Type
        {
            get { return _type; }
        }

        public IReadOnlyList<LootPoolEntry> Pools
        {
            get { return _pools; }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_tableName))
            {
                _tableName = name;
            }
        }
    }
}
