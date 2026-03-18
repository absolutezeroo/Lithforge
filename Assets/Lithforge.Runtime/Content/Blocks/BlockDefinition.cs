using System.Collections.Generic;
using Lithforge.Runtime.Content.Loot;
using Lithforge.Voxel.Block;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Blocks
{
    /// <summary>
    /// Data-driven definition for a block type. Each asset becomes one or more entries in the
    /// <see cref="Lithforge.Voxel.Block.StateRegistry"/> after cartesian expansion of its properties.
    /// This is the authoritative managed representation — baked to <see cref="Lithforge.Voxel.Block.BlockStateCompact"/>
    /// at startup for Burst access.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBlock", menuName = "Lithforge/Content/Block Definition", order = 0)]
    public sealed class BlockDefinition : ScriptableObject
    {
        [FormerlySerializedAs("_namespace"),Header("Identity")]
        [Tooltip("Namespace for the resource id (e.g. 'lithforge')")]
        /// <summary>ResourceId namespace — almost always "lithforge" for base content.</summary>
        [SerializeField] private string @namespace = "lithforge";

        /// <summary>Unique name within the namespace (e.g. "stone", "oak_log"). Combined with namespace to form the ResourceId.</summary>
        [FormerlySerializedAs("_blockName"),Tooltip("Block name (e.g. 'stone')")]
        [SerializeField] private string blockName = "";

        [FormerlySerializedAs("_hardness"),Header("Gameplay")]
        [Tooltip("Time to break in seconds")]
        /// <summary>Time in seconds to break bare-handed. Tool speed profiles divide this value.</summary>
        [Min(0f)]
        [SerializeField] private double hardness = 1.0;

        /// <summary>Resistance to explosions — higher values survive bigger blasts.</summary>
        [FormerlySerializedAs("_blastResistance"),Tooltip("Resistance to explosions")]
        [Min(0f)]
        [SerializeField] private double blastResistance = 1.0;

        /// <summary>When true, mining with a weaker tool than required still breaks the block but yields no drops.</summary>
        [FormerlySerializedAs("_requiresTool"),Tooltip("Whether a tool is required to get drops")]
        [SerializeField] private bool requiresTool;

        /// <summary>Physical material category — determines which tool type mines fastest (pickaxe for stone, shovel for dirt, etc.).</summary>
        [FormerlySerializedAs("_materialType"),Tooltip("Physical material of the block (determines mining speed per tool)")]
        [SerializeField] private BlockMaterialType materialType = BlockMaterialType.Stone;

        /// <summary>Minimum tool tier needed to get drops (0 = hand, 1 = wood, 2 = stone, 3 = iron, 4 = diamond).</summary>
        [FormerlySerializedAs("_requiredToolLevel"),Tooltip("Minimum tool level required (0 = none, 1 = wood, 2 = stone...)")]
        [Min(0)]
        [SerializeField] private int requiredToolLevel = 0;

        /// <summary>Sound group name for break/place/step audio (e.g. "stone", "wood", "grass").</summary>
        [FormerlySerializedAs("_soundGroup"),Tooltip("Sound group for block sounds")]
        [SerializeField] private string soundGroup = "stone";

        /// <summary>AABB shape used by VoxelCollider. None = passthrough (air, torches).</summary>
        [FormerlySerializedAs("_collisionShape"),Header("Physics")]
        [Tooltip("Collision shape type")]
        [SerializeField] private CollisionShapeType collisionShape = CollisionShapeType.FullCube;

        /// <summary>Rendering pass — controls submesh assignment and alpha handling.</summary>
        [FormerlySerializedAs("_renderLayer"),Header("Rendering")]
        [Tooltip("Render layer for transparency sorting")]
        [SerializeField] private RenderLayerType renderLayer = RenderLayerType.Opaque;

        /// <summary>Marks this as a fluid block (water, lava). Lowers the top face by 0.125 blocks for the fluid surface effect.</summary>
        [FormerlySerializedAs("_isFluid"),Tooltip("Whether this block is a fluid (water, lava) — affects top-face height")]
        [SerializeField] private bool isFluid;

        /// <summary>
        /// Biome tint applied to all faces when the model has no per-face tintIndex.
        /// -1 = defer to model, 0 = none, 1 = grass, 2 = foliage, 3 = water.
        /// </summary>
        [FormerlySerializedAs("_defaultTintType"),Tooltip("Default biome tint type applied to all faces when the model has no per-face tintIndex. " +
                                                          "-1 = use model tintIndex only (default), 0 = no tint, 1 = grass colormap, " +
                                                          "2 = foliage colormap, 3 = water (per-biome color)")]
        [Range(-1, 3)]
        [SerializeField] private int defaultTintType = -1;

        /// <summary>Block light emission level (0–15). Torch = 14, glowstone = 15.</summary>
        [FormerlySerializedAs("_lightEmission"),Tooltip("Light emitted by this block (0-15)")]
        [Range(0, 15)]
        [SerializeField] private int lightEmission;

        /// <summary>Light absorbed when passing through this block (0–15). 15 = fully opaque, 0 = transparent.</summary>
        [FormerlySerializedAs("_lightFilter"),Tooltip("Light absorbed by this block (0-15)")]
        [Range(0, 15)]
        [SerializeField] private int lightFilter = 15;

        /// <summary>Hex color for the world map display (#RRGGBB or #RRGGBBAA).</summary>
        [FormerlySerializedAs("_mapColor"),Tooltip("Color shown on the map (#RRGGBB or #RRGGBBAA)")]
        [SerializeField] private string mapColor = "#808080";

        /// <summary>Loot table rolled when this block is broken. Null means no drops.</summary>
        [FormerlySerializedAs("_lootTable"),Header("References")]
        [Tooltip("Loot table for this block")]
        [SerializeField] private LootTable lootTable;

        /// <summary>Maps property combinations to model variants (e.g. axis=y → upright log model).</summary>
        [FormerlySerializedAs("_blockStateMapping"),Tooltip("Block state mapping (variants)")]
        [SerializeField] private BlockStateMapping blockStateMapping;

        /// <summary>
        /// Block state properties (axis, facing, lit, etc.). The cartesian product of all property values
        /// determines how many states this block registers in the StateRegistry.
        /// </summary>
        [FormerlySerializedAs("_properties"),Header("Properties")]
        [Tooltip("Block state properties (axis, facing, lit, etc.)")]
        [SerializeField] private List<BlockPropertyEntry> properties = new();

        /// <summary>Tag string IDs this block belongs to (e.g. "mineable_pickaxe", "logs").</summary>
        [FormerlySerializedAs("_tags"),Header("Tags")]
        [Tooltip("Tags this block belongs to (string ids for backward compatibility)")]
        [SerializeField] private List<string> tags = new();

        /// <summary>ResourceId namespace — almost always "lithforge" for base content.</summary>
        public string Namespace
        {
            get { return @namespace; }
        }

        /// <summary>Unique name within the namespace (e.g. "stone", "oak_log").</summary>
        public string BlockName
        {
            get { return blockName; }
        }

        /// <summary>Time in seconds to break bare-handed. Tool speed profiles divide this value.</summary>
        public double Hardness
        {
            get { return hardness; }
        }

        /// <summary>Resistance to explosions — higher values survive bigger blasts.</summary>
        public double BlastResistance
        {
            get { return blastResistance; }
        }

        /// <summary>When true, mining with a weaker tool yields no drops even though the block breaks.</summary>
        public bool RequiresTool
        {
            get { return requiresTool; }
        }

        /// <summary>Physical material category — determines which tool type mines fastest.</summary>
        public BlockMaterialType MaterialType
        {
            get { return materialType; }
        }

        /// <summary>Minimum tool tier needed to get drops (0 = hand, 1 = wood, 2 = stone, 3 = iron, 4 = diamond).</summary>
        public int RequiredToolLevel
        {
            get { return requiredToolLevel; }
        }

        /// <summary>Sound group name for break/place/step audio.</summary>
        public string SoundGroup
        {
            get { return soundGroup; }
        }

        /// <summary>AABB shape used by VoxelCollider.</summary>
        public CollisionShapeType CollisionShape
        {
            get { return collisionShape; }
        }

        /// <summary>Rendering pass — controls submesh assignment and alpha handling.</summary>
        public RenderLayerType RenderLayer
        {
            get { return renderLayer; }
        }

        /// <summary>True for fluid blocks (water, lava) — lowers top face for the surface effect.</summary>
        public bool IsFluid
        {
            get { return isFluid; }
        }

        /// <summary>Biome tint type: -1 = defer to model, 0 = none, 1 = grass, 2 = foliage, 3 = water.</summary>
        public int DefaultTintType
        {
            get { return defaultTintType; }
        }

        /// <summary>Block light emission level (0–15).</summary>
        public int LightEmission
        {
            get { return lightEmission; }
        }

        /// <summary>Light absorbed when passing through (0–15). 15 = fully opaque.</summary>
        public int LightFilter
        {
            get { return lightFilter; }
        }

        /// <summary>Hex color for the world map display.</summary>
        public string MapColor
        {
            get { return mapColor; }
        }

        /// <summary>Loot table rolled when this block is broken. Null means no drops.</summary>
        public LootTable LootTable
        {
            get { return lootTable; }
        }

        /// <summary>Maps property combinations to model variants.</summary>
        public BlockStateMapping BlockStateMapping
        {
            get { return blockStateMapping; }
        }

        /// <summary>Block state properties whose cartesian product determines registered state count.</summary>
        public IReadOnlyList<BlockPropertyEntry> Properties
        {
            get { return properties; }
        }

        /// <summary>Tag string IDs this block belongs to.</summary>
        public IReadOnlyList<string> Tags
        {
            get { return tags; }
        }

        /// <summary>Collision shape as a lowercase string for serialization (e.g. "full_cube", "none").</summary>
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

        /// <summary>Render layer as a lowercase string for serialization (e.g. "opaque", "cutout").</summary>
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

        /// <summary>
        /// Returns the total number of block states this definition will register —
        /// the cartesian product of all property value counts (1 if no properties).
        /// </summary>
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
