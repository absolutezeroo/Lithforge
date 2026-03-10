using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [CreateAssetMenu(fileName = "NewBlock", menuName = "Lithforge/Content/Block Definition", order = 0)]
    public sealed class BlockDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Namespace for the resource id (e.g. 'lithforge')")]
        [SerializeField] private string _namespace = "lithforge";

        [Tooltip("Block name (e.g. 'stone')")]
        [SerializeField] private string _blockName = "";

        [Header("Gameplay")]
        [Tooltip("Time to break in seconds")]
        [Min(0f)]
        [SerializeField] private double _hardness = 1.0;

        [Tooltip("Resistance to explosions")]
        [Min(0f)]
        [SerializeField] private double _blastResistance = 1.0;

        [Tooltip("Whether a tool is required to get drops")]
        [SerializeField] private bool _requiresTool;

        [Tooltip("Sound group for block sounds")]
        [SerializeField] private string _soundGroup = "stone";

        [Header("Physics")]
        [Tooltip("Collision shape type")]
        [SerializeField] private CollisionShapeType _collisionShape = CollisionShapeType.FullCube;

        [Header("Rendering")]
        [Tooltip("Render layer for transparency sorting")]
        [SerializeField] private RenderLayerType _renderLayer = RenderLayerType.Opaque;

        [Tooltip("Light emitted by this block (0-15)")]
        [Range(0, 15)]
        [SerializeField] private int _lightEmission;

        [Tooltip("Light absorbed by this block (0-15)")]
        [Range(0, 15)]
        [SerializeField] private int _lightFilter = 15;

        [Tooltip("Color shown on the map (#RRGGBB or #RRGGBBAA)")]
        [SerializeField] private string _mapColor = "#808080";

        [Header("References")]
        [Tooltip("Loot table for this block")]
        [SerializeField] private LootTableSO _lootTable;

        [Tooltip("Block state mapping (variants)")]
        [SerializeField] private BlockStateMappingSO _blockStateMapping;

        [Header("Properties")]
        [Tooltip("Block state properties (axis, facing, lit, etc.)")]
        [SerializeField] private List<BlockPropertyEntry> _properties = new List<BlockPropertyEntry>();

        [Header("Tags")]
        [Tooltip("Tags this block belongs to (string ids for backward compatibility)")]
        [SerializeField] private List<string> _tags = new List<string>();

        public string Namespace
        {
            get { return _namespace; }
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

        public LootTableSO LootTable
        {
            get { return _lootTable; }
        }

        public BlockStateMappingSO BlockStateMapping
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
                switch (_collisionShape)
                {
                    case CollisionShapeType.None:
                        return "none";
                    case CollisionShapeType.FullCube:
                        return "full_cube";
                    case CollisionShapeType.Slab:
                        return "slab";
                    case CollisionShapeType.Stairs:
                        return "stairs";
                    case CollisionShapeType.Fence:
                        return "fence";
                    default:
                        return "full_cube";
                }
            }
        }

        public string RenderLayerString
        {
            get
            {
                switch (_renderLayer)
                {
                    case RenderLayerType.Opaque:
                        return "opaque";
                    case RenderLayerType.Cutout:
                        return "cutout";
                    case RenderLayerType.Translucent:
                        return "translucent";
                    default:
                        return "opaque";
                }
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
    }

    public enum CollisionShapeType
    {
        FullCube = 0,
        None = 1,
        Slab = 2,
        Stairs = 3,
        Fence = 4,
    }

    public enum RenderLayerType
    {
        Opaque = 0,
        Cutout = 1,
        Translucent = 2,
    }

    [System.Serializable]
    public sealed class BlockPropertyEntry
    {
        [Tooltip("Property name (e.g. 'facing', 'lit', 'axis')")]
        [SerializeField] private string _name;

        [Tooltip("Property type")]
        [SerializeField] private BlockPropertyKind _kind;

        [Tooltip("Possible values (for enum type, or auto-generated for bool/int)")]
        [SerializeField] private List<string> _values = new List<string>();

        [Tooltip("Default value")]
        [SerializeField] private string _defaultValue;

        [Tooltip("Min value (for int range type)")]
        [SerializeField] private int _minValue;

        [Tooltip("Max value (for int range type)")]
        [SerializeField] private int _maxValue;

        public string Name
        {
            get { return _name; }
        }

        public BlockPropertyKind Kind
        {
            get { return _kind; }
        }

        public IReadOnlyList<string> Values
        {
            get { return _values; }
        }

        public string DefaultValue
        {
            get { return _defaultValue; }
        }

        public int MinValue
        {
            get { return _minValue; }
        }

        public int MaxValue
        {
            get { return _maxValue; }
        }

        public int ValueCount
        {
            get
            {
                switch (_kind)
                {
                    case BlockPropertyKind.Bool:
                        return 2;
                    case BlockPropertyKind.IntRange:
                        return _maxValue - _minValue + 1;
                    case BlockPropertyKind.Enum:
                        return _values.Count;
                    default:
                        return 1;
                }
            }
        }

        public string GetValue(int index)
        {
            switch (_kind)
            {
                case BlockPropertyKind.Bool:
                    return index == 0 ? "true" : "false";
                case BlockPropertyKind.IntRange:
                    return (_minValue + index).ToString();
                case BlockPropertyKind.Enum:
                    return _values[index];
                default:
                    return _defaultValue;
            }
        }
    }

    public enum BlockPropertyKind
    {
        Bool = 0,
        IntRange = 1,
        Enum = 2,
    }
}
