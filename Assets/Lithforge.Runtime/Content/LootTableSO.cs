using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [CreateAssetMenu(fileName = "NewLootTable", menuName = "Lithforge/Content/Loot Table", order = 6)]
    public sealed class LootTableSO : ScriptableObject
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
    }

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

    [System.Serializable]
    public sealed class LootItemEntry
    {
        [Tooltip("Entry type (item, empty, loot_table)")]
        [SerializeField] private string _type = "item";

        [Tooltip("Item reference")]
        [SerializeField] private ItemDefinitionSO _item;

        [Tooltip("Item name (fallback when SO ref not available)")]
        [SerializeField] private string _itemName = "";

        [Tooltip("Selection weight")]
        [Min(1)]
        [SerializeField] private int _weight = 1;

        [Tooltip("Conditions for this entry")]
        [SerializeField] private List<LootConditionEntry> _conditions = new List<LootConditionEntry>();

        [Tooltip("Functions to apply to this entry")]
        [SerializeField] private List<LootFunctionEntry> _functions = new List<LootFunctionEntry>();

        public string Type
        {
            get { return _type; }
        }

        public ItemDefinitionSO Item
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

    [System.Serializable]
    public sealed class LootConditionEntry
    {
        [Tooltip("Condition type")]
        [SerializeField] private string _conditionType = "";

        [Tooltip("Condition parameters as key=value pairs")]
        [SerializeField] private List<StringPair> _parameters = new List<StringPair>();

        public string ConditionType
        {
            get { return _conditionType; }
        }

        public IReadOnlyList<StringPair> Parameters
        {
            get { return _parameters; }
        }
    }

    [System.Serializable]
    public sealed class LootFunctionEntry
    {
        [Tooltip("Function type")]
        [SerializeField] private string _functionType = "";

        [Tooltip("Function parameters as key=value pairs")]
        [SerializeField] private List<StringPair> _parameters = new List<StringPair>();

        public string FunctionType
        {
            get { return _functionType; }
        }

        public IReadOnlyList<StringPair> Parameters
        {
            get { return _parameters; }
        }
    }

    [System.Serializable]
    public sealed class StringPair
    {
        [SerializeField] private string _key;
        [SerializeField] private string _value;

        public string Key
        {
            get { return _key; }
        }

        public string Value
        {
            get { return _value; }
        }

        public StringPair(string key, string value)
        {
            _key = key;
            _value = value;
        }
    }
}
