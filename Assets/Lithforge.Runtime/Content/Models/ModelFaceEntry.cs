using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Models
{
    /// <summary>
    /// Texture, UV, culling, and tint data for a single face of a <see cref="ModelElement"/>.
    /// Null or empty <see cref="Texture"/> means the face is not rendered.
    /// </summary>
    [System.Serializable]
    public sealed class ModelFaceEntry
    {
        /// <summary>
        /// Texture variable reference (e.g., "#all", "#side") resolved by
        /// ContentModelResolver against the merged texture variable map.
        /// </summary>
        [FormerlySerializedAs("_texture"),Tooltip("Texture variable reference (e.g. '#all', '#side')")]
        [SerializeField] private string texture;

        /// <summary>
        /// UV region within the texture as (u1, v1, u2, v2) in 0-16 coordinates.
        /// Greedy meshing tiles these coords across merged quads using frac() in the shader.
        /// </summary>
        [FormerlySerializedAs("_uv"),Tooltip("UV coordinates [u1, v1, u2, v2]")]
        [SerializeField] private Vector4 uv;

        /// <summary>
        /// Which neighbor direction causes this face to be culled when occupied by an
        /// opaque block. None means the face is always emitted.
        /// </summary>
        [FormerlySerializedAs("_cullFace"),Tooltip("Face culling direction")]
        [SerializeField] private CullFace cullFace = CullFace.None;

        /// <summary>
        /// Clockwise texture rotation in degrees. Must be 0, 90, 180, or 270.
        /// </summary>
        [FormerlySerializedAs("_rotation"),Tooltip("Texture rotation (0, 90, 180, 270)")]
        [SerializeField] private int rotation;

        /// <summary>
        /// Biome tint channel: -1 = no tint, 0 = grass, 1 = foliage, 2+ = water.
        /// Mapped to PackedMeshVertex tint bits via MapTintIndex during atlas baking.
        /// </summary>
        [FormerlySerializedAs("_tintIndex"),Tooltip("Tint index for biome coloring (-1 = none)")]
        [SerializeField] private int tintIndex = -1;

        /// <summary>
        /// Texture variable reference (e.g., "#all") to resolve against the model's
        /// merged texture map. Null or empty means this face is not rendered.
        /// </summary>
        public string Texture
        {
            get { return texture; }
        }

        /// <summary>
        /// UV region as (u1, v1, u2, v2) in 0-16 texture coordinates.
        /// </summary>
        public Vector4 Uv
        {
            get { return uv; }
        }

        /// <summary>
        /// Neighbor direction that triggers culling of this face, or None to always render.
        /// </summary>
        public CullFace CullFace
        {
            get { return cullFace; }
        }

        /// <summary>
        /// Clockwise texture rotation in degrees (0, 90, 180, or 270).
        /// </summary>
        public int Rotation
        {
            get { return rotation; }
        }

        /// <summary>
        /// Biome tint channel index: -1 for no tint, 0 for grass, 1 for foliage, 2+ for water.
        /// </summary>
        public int TintIndex
        {
            get { return tintIndex; }
        }
    }
}
