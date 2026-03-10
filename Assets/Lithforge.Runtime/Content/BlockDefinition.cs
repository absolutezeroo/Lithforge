using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [CreateAssetMenu(fileName = "NewBlock", menuName = "Lithforge/Content/Block Definition", order = 0)]
    public sealed class BlockDefinition : ScriptableObject
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
        [SerializeField] private LootTable _lootTable;

        [Tooltip("Block state mapping (variants)")]
        [SerializeField] private BlockStateMapping _blockStateMapping;

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

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_blockName))
            {
                _blockName = name;
            }
        }
    }
}
