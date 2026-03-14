using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Loot
{
    [CreateAssetMenu(fileName = "NewLootTable", menuName = "Lithforge/Content/Loot Table", order = 6)]
    public sealed class LootTable : ScriptableObject
    {
        [FormerlySerializedAs("_namespace"),Header("Identity")]
        [Tooltip("Namespace for the resource id")]
        [SerializeField] private string @namespace = "lithforge";

        [FormerlySerializedAs("_tableName"),Tooltip("Loot table name (e.g. 'blocks/stone')")]
        [SerializeField] private string tableName = "";

        [FormerlySerializedAs("_type"),Header("Type")]
        [Tooltip("Loot table type")]
        [SerializeField] private string type = "block";

        [FormerlySerializedAs("_pools"),Header("Pools")]
        [Tooltip("Loot pools — each pool is rolled independently")]
        [SerializeField] private List<LootPoolEntry> pools = new List<LootPoolEntry>();

        public string Namespace
        {
            get { return @namespace; }
        }

        public string TableName
        {
            get { return tableName; }
        }

        public string Type
        {
            get { return type; }
        }

        public IReadOnlyList<LootPoolEntry> Pools
        {
            get { return pools; }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(tableName))
            {
                tableName = name;
            }
        }
    }
}
