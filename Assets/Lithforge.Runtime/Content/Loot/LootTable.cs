using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Loot
{
    /// <summary>
    ///     Data-driven loot table containing one or more pools, each rolled independently.
    ///     Referenced by <see cref="Blocks.BlockDefinition" /> to determine what drops when a block is broken.
    ///     Converted to <see cref="Lithforge.Voxel.Loot.LootTableDefinition" /> at load time.
    /// </summary>
    [CreateAssetMenu(fileName = "NewLootTable", menuName = "Lithforge/Content/Loot Table", order = 6)]
    public sealed class LootTable : ScriptableObject
    {
        /// <summary>ResourceId namespace.</summary>
        [FormerlySerializedAs("_namespace"), Header("Identity"), Tooltip("Namespace for the resource id"), SerializeField]
         private string @namespace = "lithforge";

        /// <summary>Table name within the namespace (e.g. "blocks/stone", "blocks/diamond_ore").</summary>
        [FormerlySerializedAs("_tableName"), Tooltip("Loot table name (e.g. 'blocks/stone')"), SerializeField]
         private string tableName = "";

        /// <summary>Table category — "block" for block drops, "entity" for mob drops, "chest" for container loot.</summary>
        [FormerlySerializedAs("_type"), Header("Type"), Tooltip("Loot table type"), SerializeField]
         private string type = "block";

        /// <summary>Pools rolled independently when this table is resolved.</summary>
        [FormerlySerializedAs("_pools"), Header("Pools"), Tooltip("Loot pools — each pool is rolled independently"), SerializeField]
         private List<LootPoolEntry> pools = new();

        /// <summary>ResourceId namespace.</summary>
        public string Namespace
        {
            get { return @namespace; }
        }

        /// <summary>Table name within the namespace (e.g. "blocks/stone", "blocks/diamond_ore").</summary>
        public string TableName
        {
            get { return tableName; }
        }

        /// <summary>Table category — "block" for block drops, "entity" for mob drops, "chest" for container loot.</summary>
        public string Type
        {
            get { return type; }
        }

        /// <summary>Pools rolled independently when this table is resolved.</summary>
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
