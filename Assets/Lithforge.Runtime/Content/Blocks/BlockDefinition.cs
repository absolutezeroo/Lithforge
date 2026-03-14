using System.Collections.Generic;
using Lithforge.Runtime.Content.Loot;
using Lithforge.Voxel.Block;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Blocks
{
    [CreateAssetMenu(fileName = "NewBlock", menuName = "Lithforge/Content/Block Definition", order = 0)]
    public sealed class BlockDefinition : ScriptableObject
    {
        [FormerlySerializedAs("_namespace"),Header("Identity")]
        [Tooltip("Namespace for the resource id (e.g. 'lithforge')")]
        [SerializeField] private string @namespace = "lithforge";

        [FormerlySerializedAs("_blockName"),Tooltip("Block name (e.g. 'stone')")]
        [SerializeField] private string blockName = "";

        [FormerlySerializedAs("_hardness"),Header("Gameplay")]
        [Tooltip("Time to break in seconds")]
        [Min(0f)]
        [SerializeField] private double hardness = 1.0;

        [FormerlySerializedAs("_blastResistance"),Tooltip("Resistance to explosions")]
        [Min(0f)]
        [SerializeField] private double blastResistance = 1.0;

        [FormerlySerializedAs("_requiresTool"),Tooltip("Whether a tool is required to get drops")]
        [SerializeField] private bool requiresTool;

        [FormerlySerializedAs("_materialType"),Tooltip("Physical material of the block (determines mining speed per tool)")]
        [SerializeField] private BlockMaterialType materialType = BlockMaterialType.Stone;

        [FormerlySerializedAs("_requiredToolLevel"),Tooltip("Minimum tool level required (0 = none, 1 = wood, 2 = stone...)")]
        [Min(0)]
        [SerializeField] private int requiredToolLevel = 0;

        [FormerlySerializedAs("_soundGroup"),Tooltip("Sound group for block sounds")]
        [SerializeField] private string soundGroup = "stone";

        [FormerlySerializedAs("_collisionShape"),Header("Physics")]
        [Tooltip("Collision shape type")]
        [SerializeField] private CollisionShapeType collisionShape = CollisionShapeType.FullCube;

        [FormerlySerializedAs("_renderLayer"),Header("Rendering")]
        [Tooltip("Render layer for transparency sorting")]
        [SerializeField] private RenderLayerType renderLayer = RenderLayerType.Opaque;

        [FormerlySerializedAs("_isFluid"),Tooltip("Whether this block is a fluid (water, lava) — affects top-face height")]
        [SerializeField] private bool isFluid;

        [FormerlySerializedAs("_defaultTintType"),Tooltip("Default biome tint type applied to all faces when the model has no per-face tintIndex. " +
                                                          "-1 = use model tintIndex only (default), 0 = no tint, 1 = grass colormap, " +
                                                          "2 = foliage colormap, 3 = water (per-biome color)")]
        [Range(-1, 3)]
        [SerializeField] private int defaultTintType = -1;

        [FormerlySerializedAs("_lightEmission"),Tooltip("Light emitted by this block (0-15)")]
        [Range(0, 15)]
        [SerializeField] private int lightEmission;

        [FormerlySerializedAs("_lightFilter"),Tooltip("Light absorbed by this block (0-15)")]
        [Range(0, 15)]
        [SerializeField] private int lightFilter = 15;

        [FormerlySerializedAs("_mapColor"),Tooltip("Color shown on the map (#RRGGBB or #RRGGBBAA)")]
        [SerializeField] private string mapColor = "#808080";

        [FormerlySerializedAs("_lootTable"),Header("References")]
        [Tooltip("Loot table for this block")]
        [SerializeField] private LootTable lootTable;

        [FormerlySerializedAs("_blockStateMapping"),Tooltip("Block state mapping (variants)")]
        [SerializeField] private BlockStateMapping blockStateMapping;

        [FormerlySerializedAs("_properties"),Header("Properties")]
        [Tooltip("Block state properties (axis, facing, lit, etc.)")]
        [SerializeField] private List<BlockPropertyEntry> properties = new List<BlockPropertyEntry>();

        [FormerlySerializedAs("_tags"),Header("Tags")]
        [Tooltip("Tags this block belongs to (string ids for backward compatibility)")]
        [SerializeField] private List<string> tags = new List<string>();

        public string Namespace
        {
            get { return @namespace; }
        }

        public string BlockName
        {
            get { return blockName; }
        }

        public double Hardness
        {
            get { return hardness; }
        }

        public double BlastResistance
        {
            get { return blastResistance; }
        }

        public bool RequiresTool
        {
            get { return requiresTool; }
        }

        public BlockMaterialType MaterialType
        {
            get { return materialType; }
        }

        public int RequiredToolLevel
        {
            get { return requiredToolLevel; }
        }

        public string SoundGroup
        {
            get { return soundGroup; }
        }

        public CollisionShapeType CollisionShape
        {
            get { return collisionShape; }
        }

        public RenderLayerType RenderLayer
        {
            get { return renderLayer; }
        }

        public bool IsFluid
        {
            get { return isFluid; }
        }

        public int DefaultTintType
        {
            get { return defaultTintType; }
        }

        public int LightEmission
        {
            get { return lightEmission; }
        }

        public int LightFilter
        {
            get { return lightFilter; }
        }

        public string MapColor
        {
            get { return mapColor; }
        }

        public LootTable LootTable
        {
            get { return lootTable; }
        }

        public BlockStateMapping BlockStateMapping
        {
            get { return blockStateMapping; }
        }

        public IReadOnlyList<BlockPropertyEntry> Properties
        {
            get { return properties; }
        }

        public IReadOnlyList<string> Tags
        {
            get { return tags; }
        }

        public string CollisionShapeString
        {
            get
            {
                return collisionShape switch
                {
                    CollisionShapeType.None => "none",
                    CollisionShapeType.FullCube => "full_cube",
                    CollisionShapeType.Slab => "slab",
                    CollisionShapeType.Stairs => "stairs",
                    CollisionShapeType.Fence => "fence",
                    _ => "full_cube",
                };
            }
        }

        public string RenderLayerString
        {
            get
            {
                return renderLayer switch
                {
                    RenderLayerType.Opaque => "opaque",
                    RenderLayerType.Cutout => "cutout",
                    RenderLayerType.Translucent => "translucent",
                    _ => "opaque",
                };
            }
        }

        public int ComputeStateCount()
        {
            if (properties.Count == 0)
            {
                return 1;
            }

            int count = 1;

            for (int i = 0; i < properties.Count; i++)
            {
                count *= properties[i].ValueCount;
            }

            return count;
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(blockName))
            {
                blockName = name;
            }
        }
    }
}
