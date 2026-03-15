using System.Collections.Generic;
using Lithforge.Runtime.Content.Items;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Loot
{
    /// <summary>
    /// A single entry in a loot pool — references an item with a selection weight,
    /// plus conditions that gate inclusion and functions that modify the resulting drop.
    /// </summary>
    [System.Serializable]
    public sealed class LootItemEntry
    {
        /// <summary>Entry type: "item" for a direct drop, "empty" for nothing, "loot_table" for delegation.</summary>
        [FormerlySerializedAs("_type"),Tooltip("Entry type (item, empty, loot_table)")]
        [SerializeField] private string type = "item";

        /// <summary>Direct item reference. Preferred over itemName when available.</summary>
        [FormerlySerializedAs("_item"),Tooltip("Item reference")]
        [SerializeField] private ItemDefinition item;

        /// <summary>Fallback item name resolved by ItemRegistry when the direct reference is null.</summary>
        [FormerlySerializedAs("_itemName"),Tooltip("Item name (fallback when direct reference not set)")]
        [SerializeField] private string itemName = "";

        /// <summary>Weighted-random selection weight relative to other entries in the same pool.</summary>
        [FormerlySerializedAs("_weight"),Tooltip("Selection weight")]
        [Min(1)]
        [SerializeField] private int weight = 1;

        /// <summary>All conditions must pass for this entry to be eligible during pool rolls.</summary>
        [FormerlySerializedAs("_conditions"),Tooltip("Conditions for this entry")]
        [SerializeField] private List<LootConditionEntry> conditions = new List<LootConditionEntry>();

        /// <summary>Functions applied to the drop after selection (e.g. set count, apply fortune).</summary>
        [FormerlySerializedAs("_functions"),Tooltip("Functions to apply to this entry")]
        [SerializeField] private List<LootFunctionEntry> functions = new List<LootFunctionEntry>();

        /// <summary>Entry type: "item" for a direct drop, "empty" for nothing, "loot_table" for delegation.</summary>
        public string Type
        {
            get { return type; }
        }

        /// <summary>Direct item reference. Preferred over ItemName when available.</summary>
        public ItemDefinition Item
        {
            get { return item; }
        }

        /// <summary>Fallback item name resolved by ItemRegistry when the direct reference is null.</summary>
        public string ItemName
        {
            get { return itemName; }
        }

        /// <summary>Weighted-random selection weight relative to other entries in the same pool.</summary>
        public int Weight
        {
            get { return weight; }
        }

        /// <summary>All conditions must pass for this entry to be eligible during pool rolls.</summary>
        public IReadOnlyList<LootConditionEntry> Conditions
        {
            get { return conditions; }
        }

        /// <summary>Functions applied to the drop after selection (e.g. set count, apply fortune).</summary>
        public IReadOnlyList<LootFunctionEntry> Functions
        {
            get { return functions; }
        }
    }
}
