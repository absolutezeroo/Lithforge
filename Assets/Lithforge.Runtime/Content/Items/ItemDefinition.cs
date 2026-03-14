using System.Collections.Generic;
using Lithforge.Runtime.Content.Blocks;
using Lithforge.Runtime.Content.Items.Affixes;
using Lithforge.Runtime.Content.Models;
using Lithforge.Runtime.Content.Tools;
using Lithforge.Voxel.Item;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Items
{
    [CreateAssetMenu(fileName = "NewItem", menuName = "Lithforge/Content/Item Definition", order = 3)]
    public sealed class ItemDefinition : ScriptableObject
    {
        [FormerlySerializedAs("_namespace"),Header("Identity")]
        [Tooltip("Namespace for the resource id")]
        [SerializeField] private string @namespace = "lithforge";

        [FormerlySerializedAs("itemName")]
        [Tooltip("Item name")]
        [SerializeField] private string _itemName = "";

        [FormerlySerializedAs("maxStackSize")]
        [Header("Stack")]
        [Tooltip("Maximum stack size")]
        [Range(1, 64)]
        [SerializeField] private int _maxStackSize = 64;

        [FormerlySerializedAs("toolType")]
        [Header("Tool Properties")]
        [Tooltip("Tool type")]
        [SerializeField] private ToolType _toolType = ToolType.None;

        [FormerlySerializedAs("toolLevel")]
        [Tooltip("Tool mining level")]
        [Min(0)]
        [SerializeField] private int _toolLevel;

        [FormerlySerializedAs("durability")]
        [Tooltip("Item durability (0 = unbreakable)")]
        [Min(0)]
        [SerializeField] private int _durability;

        [FormerlySerializedAs("attackDamage")]
        [Tooltip("Attack damage")]
        [Min(0f)]
        [SerializeField] private float _attackDamage = 1.0f;

        [FormerlySerializedAs("attackSpeed")]
        [Tooltip("Attack speed")]
        [Min(0f)]
        [SerializeField] private float _attackSpeed = 4.0f;

        [FormerlySerializedAs("miningSpeed")]
        [Tooltip("Mining speed multiplier")]
        [Min(0f)]
        [SerializeField] private float _miningSpeed = 1.0f;

        [FormerlySerializedAs("toolSpeedProfile")]
        [Tooltip("Speed profile per block material (optional, overrides MiningSpeed)")]
        [SerializeField] private ToolSpeedProfile _toolSpeedProfile;

        [FormerlySerializedAs("affixes")]
        [Tooltip("Tool affixes")]
        [SerializeField] private AffixDefinition[] _affixes
            = System.Array.Empty<AffixDefinition>();

        [FormerlySerializedAs("fuelTime")]
        [Header("Fuel")]
        [Tooltip("Fuel burn time in seconds (0 = not a fuel item)")]
        [Min(0f)]
        [SerializeField] private float _fuelTime;

        [FormerlySerializedAs("placesBlock")]
        [Header("Block Placement")]
        [Tooltip("Block this item places when used")]
        [SerializeField] private BlockDefinition _placesBlock;

        [FormerlySerializedAs("itemModel")]
        [Header("Model")]
        [Tooltip("Item model reference")]
        [SerializeField] private BlockModel _itemModel;

        [FormerlySerializedAs("tags")]
        [Header("Tags")]
        [Tooltip("Tags this item belongs to")]
        [SerializeField] private List<string> _tags = new List<string>();

        public string Namespace
        {
            get { return @namespace; }
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

        public ToolSpeedProfile ToolSpeedProfile
        {
            get { return _toolSpeedProfile; }
        }

        public AffixDefinition[] Affixes
        {
            get { return _affixes; }
        }

        public float FuelTime
        {
            get { return _fuelTime; }
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
