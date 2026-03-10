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
        [SerializeField] private string itemName = "";

        [Header("Stack")]
        [Tooltip("Maximum stack size")]
        [Range(1, 64)]
        [SerializeField] private int maxStackSize = 64;

        [Header("Tool Properties")]
        [Tooltip("Tool type")]
        [SerializeField] private ToolType toolType = ToolType.None;

        [Tooltip("Tool mining level")]
        [Min(0)]
        [SerializeField] private int toolLevel;

        [Tooltip("Item durability (0 = unbreakable)")]
        [Min(0)]
        [SerializeField] private int durability;

        [Tooltip("Attack damage")]
        [Min(0f)]
        [SerializeField] private float attackDamage = 1.0f;

        [Tooltip("Attack speed")]
        [Min(0f)]
        [SerializeField] private float attackSpeed = 4.0f;

        [Tooltip("Mining speed multiplier")]
        [Min(0f)]
        [SerializeField] private float miningSpeed = 1.0f;

        [Header("Block Placement")]
        [Tooltip("Block this item places when used")]
        [SerializeField] private BlockDefinition placesBlock;

        [Header("Model")]
        [Tooltip("Item model reference")]
        [SerializeField] private BlockModel itemModel;

        [Header("Tags")]
        [Tooltip("Tags this item belongs to")]
        [SerializeField] private List<string> tags = new List<string>();

        public string Namespace
        {
            get { return _namespace; }
        }

        public string ItemName
        {
            get { return itemName; }
        }

        public int MaxStackSize
        {
            get { return maxStackSize; }
        }

        public ToolType ToolType
        {
            get { return toolType; }
        }

        public int ToolLevel
        {
            get { return toolLevel; }
        }

        public int Durability
        {
            get { return durability; }
        }

        public float AttackDamage
        {
            get { return attackDamage; }
        }

        public float AttackSpeed
        {
            get { return attackSpeed; }
        }

        public float MiningSpeed
        {
            get { return miningSpeed; }
        }

        public BlockDefinition PlacesBlock
        {
            get { return placesBlock; }
        }

        public BlockModel ItemModel
        {
            get { return itemModel; }
        }

        public IReadOnlyList<string> Tags
        {
            get { return tags; }
        }

        public bool IsBlockItem
        {
            get { return placesBlock != null; }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(itemName))
            {
                itemName = name;
            }
        }
    }
}
