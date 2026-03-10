using System.Collections.Generic;
using Lithforge.Runtime.Content.Items;
using UnityEngine;

namespace Lithforge.Runtime.Content.Loot
{
    [System.Serializable]
    public sealed class LootItemEntry
    {
        [Tooltip("Entry type (item, empty, loot_table)")]
        [SerializeField] private string type = "item";

        [Tooltip("Item reference")]
        [SerializeField] private ItemDefinition item;

        [Tooltip("Item name (fallback when direct reference not set)")]
        [SerializeField] private string itemName = "";

        [Tooltip("Selection weight")]
        [Min(1)]
        [SerializeField] private int weight = 1;

        [Tooltip("Conditions for this entry")]
        [SerializeField] private List<LootConditionEntry> conditions = new List<LootConditionEntry>();

        [Tooltip("Functions to apply to this entry")]
        [SerializeField] private List<LootFunctionEntry> functions = new List<LootFunctionEntry>();

        public string Type
        {
            get { return type; }
        }

        public ItemDefinition Item
        {
            get { return item; }
        }

        public string ItemName
        {
            get { return itemName; }
        }

        public int Weight
        {
            get { return weight; }
        }

        public IReadOnlyList<LootConditionEntry> Conditions
        {
            get { return conditions; }
        }

        public IReadOnlyList<LootFunctionEntry> Functions
        {
            get { return functions; }
        }
    }
}
