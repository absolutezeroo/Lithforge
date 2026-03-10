using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [CreateAssetMenu(fileName = "NewBlockModel", menuName = "Lithforge/Content/Block Model", order = 2)]
    public sealed class BlockModel : ScriptableObject
    {
        [Header("Parent")]
        [Tooltip("Parent model (direct reference). Drag-drop for parent chain resolution.")]
        [SerializeField] private BlockModel _parent;

        [Tooltip("Built-in parent type (used when parent is a terminal built-in model)")]
        [SerializeField] private BuiltInParentType _builtInParent = BuiltInParentType.None;

        [Header("Textures")]
        [Tooltip("Texture variable mappings (variable name → texture path or #variable reference)")]
        [SerializeField] private List<TextureVariable> _textures = new List<TextureVariable>();

        [Header("Elements")]
        [Tooltip("Model geometry elements (optional, for custom shapes)")]
        [SerializeField] private List<ModelElement> _elements = new List<ModelElement>();

        public BlockModel Parent
        {
            get { return _parent; }
        }

        public BuiltInParentType BuiltInParent
        {
            get { return _builtInParent; }
        }

        public IReadOnlyList<TextureVariable> Textures
        {
            get { return _textures; }
        }

        public IReadOnlyList<ModelElement> Elements
        {
            get { return _elements; }
        }
    }

    public enum BuiltInParentType
    {
        None = 0,
        CubeAll = 1,
        Cube = 2,
        CubeColumn = 3,
    }

    [System.Serializable]
    public sealed class TextureVariable
    {
        [Tooltip("Variable name (e.g. 'all', 'side', 'end', 'north')")]
        [SerializeField] private string _variable;

        [Tooltip("Texture value (resource path like 'lithforge:block/stone' or #variable reference like '#all')")]
        [SerializeField] private string _value;

        public string Variable
        {
            get { return _variable; }
        }

        public string Value
        {
            get { return _value; }
        }
    }

    [System.Serializable]
    public sealed class ModelElement
    {
        [Tooltip("From corner position (0-16 range)")]
        [SerializeField] private Vector3 _from;

        [Tooltip("To corner position (0-16 range)")]
        [SerializeField] private Vector3 _to;

        [Header("Faces")]
        [SerializeField] private ModelFaceEntry _north;
        [SerializeField] private ModelFaceEntry _south;
        [SerializeField] private ModelFaceEntry _east;
        [SerializeField] private ModelFaceEntry _west;
        [SerializeField] private ModelFaceEntry _up;
        [SerializeField] private ModelFaceEntry _down;

        [Header("Rotation")]
        [SerializeField] private Vector3 _rotationOrigin;
        [SerializeField] private ModelRotationAxis _rotationAxis;
        [SerializeField] private float _rotationAngle;
        [SerializeField] private bool _rotationRescale;

        public Vector3 From
        {
            get { return _from; }
        }

        public Vector3 To
        {
            get { return _to; }
        }

        public ModelFaceEntry North
        {
            get { return _north; }
        }

        public ModelFaceEntry South
        {
            get { return _south; }
        }

        public ModelFaceEntry East
        {
            get { return _east; }
        }

        public ModelFaceEntry West
        {
            get { return _west; }
        }

        public ModelFaceEntry Up
        {
            get { return _up; }
        }

        public ModelFaceEntry Down
        {
            get { return _down; }
        }
    }

    [System.Serializable]
    public sealed class ModelFaceEntry
    {
        [Tooltip("Texture variable reference (e.g. '#all', '#side')")]
        [SerializeField] private string _texture;

        [Tooltip("UV coordinates [u1, v1, u2, v2]")]
        [SerializeField] private Vector4 _uv;

        [Tooltip("Face culling direction")]
        [SerializeField] private CullFace _cullFace = CullFace.None;

        [Tooltip("Texture rotation (0, 90, 180, 270)")]
        [SerializeField] private int _rotation;

        [Tooltip("Tint index for biome coloring (-1 = none)")]
        [SerializeField] private int _tintIndex = -1;

        public string Texture
        {
            get { return _texture; }
        }

        public Vector4 Uv
        {
            get { return _uv; }
        }

        public CullFace CullFace
        {
            get { return _cullFace; }
        }

        public int Rotation
        {
            get { return _rotation; }
        }

        public int TintIndex
        {
            get { return _tintIndex; }
        }
    }

    public enum CullFace
    {
        None = 0,
        North = 1,
        South = 2,
        East = 3,
        West = 4,
        Up = 5,
        Down = 6,
    }

    public enum ModelRotationAxis
    {
        X = 0,
        Y = 1,
        Z = 2,
    }
}
