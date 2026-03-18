using System;
using System.Collections.Generic;
using Lithforge.Runtime.Content.Blocks;
using Lithforge.Runtime.Content.Items.Affixes;
using Lithforge.Runtime.Content.Models;
using Lithforge.Item;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Items
{
    /// <summary>
    /// Data-driven definition for an item type. Covers tools, materials, block items, and fuel.
    /// Loaded by ContentPipeline and registered into <see cref="Lithforge.Item.ItemRegistry"/>.
    /// Block items are auto-generated from <see cref="Blocks.BlockDefinition"/> but can also be authored manually.
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "Lithforge/Content/Item Definition", order = 3)]
    public sealed class ItemDefinition : ScriptableObject
    {
        /// <summary>ResourceId namespace — almost always "lithforge" for base content.</summary>
        [FormerlySerializedAs("_namespace"),Header("Identity")]
        [Tooltip("Namespace for the resource id")]
        [SerializeField] private string @namespace = "lithforge";

        /// <summary>Unique name within the namespace (e.g. "diamond_pickaxe", "oak_planks").</summary>
        [FormerlySerializedAs("_itemName"),Tooltip("Item name")]
        [SerializeField] private string itemName = "";

        /// <summary>Maximum stack size in a single inventory slot (1 for tools, 64 for materials).</summary>
        [FormerlySerializedAs("_maxStackSize"),Header("Stack")]
        [Tooltip("Maximum stack size")]
        [Range(1, 64)]
        [SerializeField] private int maxStackSize = 64;

        /// <summary>Which tool category this item belongs to (None, Pickaxe, Axe, Shovel, Sword, Hoe).</summary>
        [FormerlySerializedAs("_toolType"),Header("Tool Properties")]
        [Tooltip("Tool type")]
        [HideInInspector]
        [SerializeField] private ToolType toolType = ToolType.None;

        /// <summary>Mining tier (0 = hand, 1 = wood, 2 = stone, 3 = iron, 4 = diamond). Determines which blocks yield drops.</summary>
        [FormerlySerializedAs("_toolLevel"),Tooltip("Tool mining level")]
        [Min(0)]
        [HideInInspector]
        [SerializeField] private int toolLevel;

        /// <summary>Total durability before the tool breaks. 0 = indestructible.</summary>
        [FormerlySerializedAs("_durability"),Tooltip("Item durability (0 = unbreakable)")]
        [Min(0)]
        [HideInInspector]
        [SerializeField] private int durability;

        /// <summary>Base damage dealt to entities on hit.</summary>
        [FormerlySerializedAs("_attackDamage"),Tooltip("Attack damage")]
        [Min(0f)]
        [HideInInspector]
        [SerializeField] private float attackDamage = 1.0f;

        /// <summary>Base mining speed multiplier.</summary>
        [FormerlySerializedAs("_miningSpeed"),Tooltip("Mining speed multiplier")]
        [Min(0f)]
        [HideInInspector]
        [SerializeField] private float miningSpeed = 1.0f;

        /// <summary>Passive modifiers applied while this item is held (e.g. efficiency, fortune).</summary>
        [FormerlySerializedAs("_affixes"),Tooltip("Tool affixes")]
        [SerializeField] private AffixDefinition[] affixes
            = System.Array.Empty<AffixDefinition>();

        /// <summary>Burn time in seconds when used as furnace fuel. 0 = not a fuel item.</summary>
        [FormerlySerializedAs("_fuelTime"),Header("Fuel")]
        [Tooltip("Fuel burn time in seconds (0 = not a fuel item)")]
        [Min(0f)]
        [SerializeField] private float fuelTime;

        /// <summary>Block placed when this item is used. Null for non-block items (tools, materials).</summary>
        [FormerlySerializedAs("_placesBlock"),Header("Block Placement")]
        [Tooltip("Block this item places when used")]
        [SerializeField] private BlockDefinition placesBlock;

        /// <summary>3D model used for held-item rendering and item entity display.</summary>
        [FormerlySerializedAs("_itemModel"),Header("Model")]
        [Tooltip("Item model reference")]
        [SerializeField] private BlockModel itemModel;

        /// <summary>Tag string IDs this item belongs to.</summary>
        [FormerlySerializedAs("_tags"),Header("Tags")]
        [Tooltip("Tags this item belongs to")]
        [SerializeField] private List<string> tags = new();

        /// <summary>ResourceId namespace — almost always "lithforge" for base content.</summary>
        public string Namespace
        {
            get { return @namespace; }
        }

        /// <summary>Unique name within the namespace (e.g. "diamond_pickaxe", "oak_planks").</summary>
        public string ItemName
        {
            get { return itemName; }
        }

        /// <summary>Maximum stack size in a single inventory slot (1 for tools, 64 for materials).</summary>
        public int MaxStackSize
        {
            get { return maxStackSize; }
        }

        /// <summary>Which tool category this item belongs to (None, Pickaxe, Axe, Shovel, Sword, Hoe).</summary>
        [Obsolete("Use modular tool system (ToolInstance via CustomData)")]
        public ToolType ToolType
        {
            get { return toolType; }
        }

        /// <summary>Mining tier (0 = hand, 1 = wood, 2 = stone, 3 = iron, 4 = diamond). Determines which blocks yield drops.</summary>
        [Obsolete("Use modular tool system (ToolInstance via CustomData)")]
        public int ToolLevel
        {
            get { return toolLevel; }
        }

        /// <summary>Total durability before the tool breaks. 0 = indestructible.</summary>
        [Obsolete("Use modular tool system (ToolInstance via CustomData)")]
        public int Durability
        {
            get { return durability; }
        }

        /// <summary>Base damage dealt to entities on hit.</summary>
        [Obsolete("Use modular tool system (ToolInstance via CustomData)")]
        public float AttackDamage
        {
            get { return attackDamage; }
        }

        /// <summary>Base mining speed multiplier.</summary>
        [Obsolete("Use modular tool system (ToolInstance via CustomData)")]
        public float MiningSpeed
        {
            get { return miningSpeed; }
        }

        /// <summary>Passive modifiers applied while this item is held (e.g. efficiency, fortune).</summary>
        [Obsolete("Affix system has no assets and is unused. May be reactivated later.")]
        public AffixDefinition[] Affixes
        {
            get { return affixes; }
        }

        /// <summary>Burn time in seconds when used as furnace fuel. 0 = not a fuel item.</summary>
        public float FuelTime
        {
            get { return fuelTime; }
        }

        /// <summary>Block placed when this item is used. Null for non-block items (tools, materials).</summary>
        public BlockDefinition PlacesBlock
        {
            get { return placesBlock; }
        }

        /// <summary>3D model used for held-item rendering and item entity display.</summary>
        public BlockModel ItemModel
        {
            get { return itemModel; }
        }

        /// <summary>Tag string IDs this item belongs to.</summary>
        public IReadOnlyList<string> Tags
        {
            get { return tags; }
        }

        /// <summary>True if this item places a block when used (i.e. PlacesBlock is not null).</summary>
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
