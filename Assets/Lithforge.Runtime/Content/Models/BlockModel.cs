using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Models
{
    [CreateAssetMenu(fileName = "NewBlockModel", menuName = "Lithforge/Content/Block Model", order = 2)]
    public sealed class BlockModel : ScriptableObject
    {
        [FormerlySerializedAs("_parent"),Header("Parent")]
        [Tooltip("Parent model (direct reference). Drag-drop for parent chain resolution.")]
        [SerializeField] private BlockModel parent;

        [FormerlySerializedAs("_builtInParent"),Tooltip("Built-in parent type (used when parent is a terminal built-in model)")]
        [SerializeField] private BuiltInParentType builtInParent = BuiltInParentType.None;

        [FormerlySerializedAs("_textures"),Header("Textures")]
        [Tooltip("Texture variable mappings (variable name → texture path or #variable reference)")]
        [SerializeField] private List<TextureVariable> textures = new List<TextureVariable>();

        [FormerlySerializedAs("_elements"),Header("Elements")]
        [Tooltip("Model geometry elements (optional, for custom shapes)")]
        [SerializeField] private List<ModelElement> elements = new List<ModelElement>();

        [FormerlySerializedAs("_firstPersonRightHand"),Header("Display")]
        [Tooltip("First-person right hand display transform (rotation, translation, scale).")]
        [SerializeField] private ModelDisplayTransform firstPersonRightHand;

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

        public ModelDisplayTransform FirstPersonRightHand
        {
            get { return firstPersonRightHand; }
        }
    }
}
