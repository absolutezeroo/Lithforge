using System.Collections.Generic;
using Lithforge.Runtime.Content.Loot;
using UnityEngine;

namespace Lithforge.Runtime.Content.Blocks
{
    [CreateAssetMenu(fileName = "NewBlock", menuName = "Lithforge/Content/Block Definition", order = 0)]
    public sealed class BlockDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Namespace for the resource id (e.g. 'lithforge')")]
        [SerializeField] private string _namespace = "lithforge";

        [Tooltip("Block name (e.g. 'stone')")]
        [SerializeField] private string blockName = "";

        [Header("Gameplay")]
        [Tooltip("Time to break in seconds")]
        [Min(0f)]
        [SerializeField] private double hardness = 1.0;

        [Tooltip("Resistance to explosions")]
        [Min(0f)]
        [SerializeField] private double blastResistance = 1.0;

        [Tooltip("Whether a tool is required to get drops")]
        [SerializeField] private bool requiresTool;

        [Tooltip("Sound group for block sounds")]
        [SerializeField] private string soundGroup = "stone";

        [Header("Physics")]
        [Tooltip("Collision shape type")]
        [SerializeField] private CollisionShapeType collisionShape = CollisionShapeType.FullCube;

        [Header("Rendering")]
        [Tooltip("Render layer for transparency sorting")]
        [SerializeField] private RenderLayerType renderLayer = RenderLayerType.Opaque;

        [Tooltip("Whether this block is a fluid (water, lava) — affects top-face height")]
        [SerializeField] private bool isFluid;

        [Tooltip("Light emitted by this block (0-15)")]
        [Range(0, 15)]
        [SerializeField] private int lightEmission;

        [Tooltip("Light absorbed by this block (0-15)")]
        [Range(0, 15)]
        [SerializeField] private int lightFilter = 15;

        [Tooltip("Color shown on the map (#RRGGBB or #RRGGBBAA)")]
        [SerializeField] private string mapColor = "#808080";

        [Header("References")]
        [Tooltip("Loot table for this block")]
        [SerializeField] private LootTable lootTable;

        [Tooltip("Block state mapping (variants)")]
        [SerializeField] private BlockStateMapping blockStateMapping;

        [Header("Properties")]
        [Tooltip("Block state properties (axis, facing, lit, etc.)")]
        [SerializeField] private List<BlockPropertyEntry> properties = new List<BlockPropertyEntry>();

        [Header("Tags")]
        [Tooltip("Tags this block belongs to (string ids for backward compatibility)")]
        [SerializeField] private List<string> tags = new List<string>();

        public string Namespace
        {
            get { return _namespace; }
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
