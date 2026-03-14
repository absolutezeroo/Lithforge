using System.Collections.Generic;
using Lithforge.Runtime.Content.Items;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Loot
{
    [System.Serializable]
    public sealed class LootItemEntry
    {
        [FormerlySerializedAs("type")]
        [Tooltip("Entry type (item, empty, loot_table)")]
        [SerializeField] private string _type = "item";

        [FormerlySerializedAs("item")]
        [Tooltip("Item reference")]
        [SerializeField] private ItemDefinition _item;

        [FormerlySerializedAs("itemName")]
        [Tooltip("Item name (fallback when direct reference not set)")]
        [SerializeField] private string _itemName = "";

        [FormerlySerializedAs("weight")]
        [Tooltip("Selection weight")]
        [Min(1)]
        [SerializeField] private int _weight = 1;

        [FormerlySerializedAs("conditions")]
        [Tooltip("Conditions for this entry")]
        [SerializeField] private List<LootConditionEntry> _conditions = new List<LootConditionEntry>();

        [FormerlySerializedAs("functions")]
        [Tooltip("Functions to apply to this entry")]
        [SerializeField] private List<LootFunctionEntry> _functions = new List<LootFunctionEntry>();

        public string Type
        {
            get { return _type; }
        }

        public ItemDefinition Item
        {
            get { return _item; }
        }

        public string ItemName
        {
            get { return _itemName; }
        }

        public int Weight
        {
            get { return _weight; }
        }

        public IReadOnlyList<LootConditionEntry> Conditions
        {
            get { return _conditions; }
        }

        public IReadOnlyList<LootFunctionEntry> Functions
        {
            get { return _functions; }
        }
    }
}
