using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Models
{
    /// <summary>
    ///     Defines the visual geometry and textures for a block or item, following a
    ///     Minecraft-style model system with parent inheritance. ContentModelResolver
    ///     walks the parent chain during ContentPipeline Phase 3 to merge textures and
    ///     elements from root to leaf (child overrides parent).
    /// </summary>
    /// <remarks>
    ///     Models form a DAG through <see cref="Parent" /> references, terminating at a
    ///     <see cref="BuiltInParentType" /> which provides the default face-to-variable mapping.
    ///     Circular parent chains are detected and logged by ContentModelResolver.
    /// </remarks>
    [CreateAssetMenu(fileName = "NewBlockModel", menuName = "Lithforge/Content/Block Model", order = 2)]
    public sealed class BlockModel : ScriptableObject
    {
        /// <summary>
        ///     Next model in the inheritance chain. Textures and elements are merged
        ///     from root ancestor to this leaf, with child values overriding parent.
        /// </summary>
        [FormerlySerializedAs("_parent"), Header("Parent"), Tooltip("Parent model (direct reference). Drag-drop for parent chain resolution."), SerializeField]
         private BlockModel parent;

        /// <summary>
        ///     Terminal built-in parent that defines the face-to-texture-variable mapping
        ///     (e.g., CubeAll maps all six faces to the "all" variable). Set to None when
        ///     this model inherits from another BlockModel asset instead.
        /// </summary>
        [FormerlySerializedAs("_builtInParent"), Tooltip("Built-in parent type (used when parent is a terminal built-in model)"), SerializeField]
         private BuiltInParentType builtInParent = BuiltInParentType.None;

        /// <summary>
        ///     Named texture slots that map variable names (e.g., "all", "side", "end") to
        ///     Texture2D assets or to other variables via "#variable" indirection.
        /// </summary>
        [FormerlySerializedAs("_textures"), Header("Textures"), Tooltip("Texture variable mappings (variable name → texture path or #variable reference)"), SerializeField]
         private List<TextureVariable> textures = new();

        /// <summary>
        ///     Cuboid sub-meshes that define custom geometry. If a child model provides
        ///     elements, they replace the parent's elements entirely rather than merging.
        ///     Empty for models that rely solely on their built-in parent's default cube.
        /// </summary>
        [FormerlySerializedAs("_elements"), Header("Elements"), Tooltip("Model geometry elements (optional, for custom shapes)"), SerializeField]
         private List<ModelElement> elements = new();

        /// <summary>
        ///     How to position, rotate, and scale this model when held in the player's
        ///     right hand in first-person view. Resolved by walking the parent chain
        ///     until a transform with HasValue is found.
        /// </summary>
        [FormerlySerializedAs("_firstPersonRightHand"), Header("Display"), Tooltip("First-person right hand display transform (rotation, translation, scale)."), SerializeField]
         private ModelDisplayTransform firstPersonRightHand;

        /// <summary>
        ///     Next model in the inheritance chain, or null if this is a root model.
        /// </summary>
        public BlockModel Parent
        {
            get { return parent; }
        }

        /// <summary>
        ///     Terminal built-in parent type that determines face-to-variable mapping.
        /// </summary>
        public BuiltInParentType BuiltInParent
        {
            get { return builtInParent; }
        }

        /// <summary>
        ///     Named texture variable mappings defined on this model (not yet merged with parents).
        /// </summary>
        public IReadOnlyList<TextureVariable> Textures
        {
            get { return textures; }
        }

        /// <summary>
        ///     Cuboid geometry elements defined on this model. Empty means inherit from parent.
        /// </summary>
        public IReadOnlyList<ModelElement> Elements
        {
            get { return elements; }
        }

        /// <summary>
        ///     First-person right-hand display transform, or null/HasValue=false if unset.
        /// </summary>
        public ModelDisplayTransform FirstPersonRightHand
        {
            get { return firstPersonRightHand; }
        }
    }
}
