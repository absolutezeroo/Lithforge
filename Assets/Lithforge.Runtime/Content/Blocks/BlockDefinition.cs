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

        [FormerlySerializedAs("blockName")]
        [Tooltip("Block name (e.g. 'stone')")]
        [SerializeField] private string _blockName = "";

        [Header("Gameplay")]
        [FormerlySerializedAs("hardness")]
        [Tooltip("Time to break in seconds")]
        [Min(0f)]
        [SerializeField] private double _hardness = 1.0;

        [FormerlySerializedAs("blastResistance")]
        [Tooltip("Resistance to explosions")]
        [Min(0f)]
        [SerializeField] private double _blastResistance = 1.0;

        [FormerlySerializedAs("requiresTool")]
        [Tooltip("Whether a tool is required to get drops")]
        [SerializeField] private bool _requiresTool;

        [FormerlySerializedAs("materialType")]
        [Tooltip("Physical material of the block (determines mining speed per tool)")]
        [SerializeField] private BlockMaterialType _materialType = BlockMaterialType.Stone;

        [FormerlySerializedAs("requiredToolLevel")]
        [Tooltip("Minimum tool level required (0 = none, 1 = wood, 2 = stone...)")]
        [Min(0)]
        [SerializeField] private int _requiredToolLevel = 0;

        [FormerlySerializedAs("soundGroup")]
        [Tooltip("Sound group for block sounds")]
        [SerializeField] private string _soundGroup = "stone";

        [Header("Physics")]
        [FormerlySerializedAs("collisionShape")]
        [Tooltip("Collision shape type")]
        [SerializeField] private CollisionShapeType _collisionShape = CollisionShapeType.FullCube;

        [Header("Rendering")]
        [FormerlySerializedAs("renderLayer")]
        [Tooltip("Render layer for transparency sorting")]
        [SerializeField] private RenderLayerType _renderLayer = RenderLayerType.Opaque;

        [FormerlySerializedAs("isFluid")]
        [Tooltip("Whether this block is a fluid (water, lava) — affects top-face height")]
        [SerializeField] private bool _isFluid;

        [FormerlySerializedAs("defaultTintType")]
        [Tooltip("Default biome tint type applied to all faces when the model has no per-face tintIndex. " +
                 "-1 = use model tintIndex only (default), 0 = no tint, 1 = grass colormap, " +
                 "2 = foliage colormap, 3 = water (per-biome color)")]
        [Range(-1, 3)]
        [SerializeField] private int _defaultTintType = -1;

        [FormerlySerializedAs("lightEmission")]
        [Tooltip("Light emitted by this block (0-15)")]
        [Range(0, 15)]
        [SerializeField] private int _lightEmission;

        [FormerlySerializedAs("lightFilter")]
        [Tooltip("Light absorbed by this block (0-15)")]
        [Range(0, 15)]
        [SerializeField] private int _lightFilter = 15;

        [FormerlySerializedAs("mapColor")]
        [Tooltip("Color shown on the map (#RRGGBB or #RRGGBBAA)")]
        [SerializeField] private string _mapColor = "#808080";

        [Header("References")]
        [FormerlySerializedAs("lootTable")]
        [Tooltip("Loot table for this block")]
        [SerializeField] private LootTable _lootTable;

        [FormerlySerializedAs("blockStateMapping")]
        [Tooltip("Block state mapping (variants)")]
        [SerializeField] private BlockStateMapping _blockStateMapping;

        [Header("Properties")]
        [FormerlySerializedAs("properties")]
        [Tooltip("Block state properties (axis, facing, lit, etc.)")]
        [SerializeField] private List<BlockPropertyEntry> _properties = new List<BlockPropertyEntry>();

        [Header("Tags")]
        [FormerlySerializedAs("tags")]
        [Tooltip("Tags this block belongs to (string ids for backward compatibility)")]
        [SerializeField] private List<string> _tags = new List<string>();

        public string Namespace
        {
            get { return @namespace; }
        }

        public string BlockName
        {
            get { return _blockName; }
        }

        public double Hardness
        {
            get { return _hardness; }
        }

        public double BlastResistance
        {
            get { return _blastResistance; }
        }

        public bool RequiresTool
        {
            get { return _requiresTool; }
        }

        public BlockMaterialType MaterialType
        {
            get { return _materialType; }
        }

        public int RequiredToolLevel
        {
            get { return _requiredToolLevel; }
        }

        public string SoundGroup
        {
            get { return _soundGroup; }
        }

        public CollisionShapeType CollisionShape
        {
            get { return _collisionShape; }
        }

        public RenderLayerType RenderLayer
        {
            get { return _renderLayer; }
        }

        public bool IsFluid
        {
            get { return _isFluid; }
        }

        public int DefaultTintType
        {
            get { return _defaultTintType; }
        }

        public int LightEmission
        {
            get { return _lightEmission; }
        }

        public int LightFilter
        {
            get { return _lightFilter; }
        }

        public string MapColor
        {
            get { return _mapColor; }
        }

        public LootTable LootTable
        {
            get { return _lootTable; }
        }

        public BlockStateMapping BlockStateMapping
        {
            get { return _blockStateMapping; }
        }

        public IReadOnlyList<BlockPropertyEntry> Properties
        {
            get { return _properties; }
        }

        public IReadOnlyList<string> Tags
        {
            get { return _tags; }
        }

        public string CollisionShapeString
        {
            get
            {
                return _collisionShape switch
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
                return _renderLayer switch
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
            if (_properties.Count == 0)
            {
                return 1;
            }

            int count = 1;

            for (int i = 0; i < _properties.Count; i++)
            {
                count *= _properties[i].ValueCount;
            }

            return count;
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_blockName))
            {
                _blockName = name;
            }
        }
    }
}
