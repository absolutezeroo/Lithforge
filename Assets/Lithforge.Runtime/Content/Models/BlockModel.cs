using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content.Models
{
    [CreateAssetMenu(fileName = "NewBlockModel", menuName = "Lithforge/Content/Block Model", order = 2)]
    public sealed class BlockModel : ScriptableObject
    {
        [Header("Parent")]
        [Tooltip("Parent model (direct reference). Drag-drop for parent chain resolution.")]
        [SerializeField] private BlockModel parent;

        [Tooltip("Built-in parent type (used when parent is a terminal built-in model)")]
        [SerializeField] private BuiltInParentType builtInParent = BuiltInParentType.None;

        [Header("Textures")]
        [Tooltip("Texture variable mappings (variable name → texture path or #variable reference)")]
        [SerializeField] private List<TextureVariable> textures = new List<TextureVariable>();

        [Header("Elements")]
        [Tooltip("Model geometry elements (optional, for custom shapes)")]
        [SerializeField] private List<ModelElement> elements = new List<ModelElement>();

        public BlockModel Parent
        {
            get { return parent; }
        }

        public BuiltInParentType BuiltInParent
        {
            get { return builtInParent; }
        }

        public IReadOnlyList<TextureVariable> Textures
        {
            get { return textures; }
        }

        public IReadOnlyList<ModelElement> Elements
        {
            get { return elements; }
        }
    }
}
