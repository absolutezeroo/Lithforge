using System.Collections.Generic;
using Lithforge.Voxel.Item;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [CreateAssetMenu(fileName = "NewItem", menuName = "Lithforge/Content/Item Definition", order = 3)]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Namespace for the resource id")]
        [SerializeField] private string _namespace = "lithforge";

        [Tooltip("Item name")]
        [SerializeField] private string _itemName = "";

        [Header("Stack")]
        [Tooltip("Maximum stack size")]
        [Range(1, 64)]
        [SerializeField] private int _maxStackSize = 64;

        [Header("Tool Properties")]
        [Tooltip("Tool type")]
        [SerializeField] private ToolType _toolType = ToolType.None;

        [Tooltip("Tool mining level")]
        [Min(0)]
        [SerializeField] private int _toolLevel;

        [Tooltip("Item durability (0 = unbreakable)")]
        [Min(0)]
        [SerializeField] private int _durability;

        [Tooltip("Attack damage")]
        [Min(0f)]
        [SerializeField] private float _attackDamage = 1.0f;

        [Tooltip("Attack speed")]
        [Min(0f)]
        [SerializeField] private float _attackSpeed = 4.0f;

        [Tooltip("Mining speed multiplier")]
        [Min(0f)]
        [SerializeField] private float _miningSpeed = 1.0f;

        [Header("Block Placement")]
        [Tooltip("Block this item places when used")]
        [SerializeField] private BlockDefinition _placesBlock;

        [Header("Model")]
        [Tooltip("Item model reference")]
        [SerializeField] private BlockModel _itemModel;

        [Header("Tags")]
        [Tooltip("Tags this item belongs to")]
        [SerializeField] private List<string> _tags = new List<string>();

        public string Namespace
        {
            get { return _namespace; }
        }

        public string ItemName
        {
            get { return _itemName; }
        }

        public int MaxStackSize
        {
            get { return _maxStackSize; }
        }

        public ToolType ToolType
        {
            get { return _toolType; }
        }

        public int ToolLevel
        {
            get { return _toolLevel; }
        }

        public int Durability
        {
            get { return _durability; }
        }

        public float AttackDamage
        {
            get { return _attackDamage; }
        }

        public float AttackSpeed
        {
            get { return _attackSpeed; }
        }

        public float MiningSpeed
        {
            get { return _miningSpeed; }
        }

        public BlockDefinition PlacesBlock
        {
            get { return _placesBlock; }
        }

        public BlockModel ItemModel
        {
            get { return _itemModel; }
        }

        public IReadOnlyList<string> Tags
        {
            get { return _tags; }
        }

        public bool IsBlockItem
        {
            get { return _placesBlock != null; }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_itemName))
            {
                _itemName = name;
            }
        }
    }
}
