using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Models
{
    [CreateAssetMenu(fileName = "NewBlockModel", menuName = "Lithforge/Content/Block Model", order = 2)]
    public sealed class BlockModel : ScriptableObject
    {
        [Header("Parent")]
        [FormerlySerializedAs("parent")]
        [Tooltip("Parent model (direct reference). Drag-drop for parent chain resolution.")]
        [SerializeField] private BlockModel _parent;

        [FormerlySerializedAs("builtInParent")]
        [Tooltip("Built-in parent type (used when parent is a terminal built-in model)")]
        [SerializeField] private BuiltInParentType _builtInParent = BuiltInParentType.None;

        [Header("Textures")]
        [FormerlySerializedAs("textures")]
        [Tooltip("Texture variable mappings (variable name → texture path or #variable reference)")]
        [SerializeField] private List<TextureVariable> _textures = new List<TextureVariable>();

        [Header("Elements")]
        [FormerlySerializedAs("elements")]
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
}
