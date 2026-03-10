using UnityEngine;

namespace Lithforge.Runtime.Content.Models
{
    [System.Serializable]
    public sealed class ModelFaceEntry
    {
        [Tooltip("Texture variable reference (e.g. '#all', '#side')")]
        [SerializeField] private string texture;

        [Tooltip("UV coordinates [u1, v1, u2, v2]")]
        [SerializeField] private Vector4 uv;

        [Tooltip("Face culling direction")]
        [SerializeField] private CullFace cullFace = CullFace.None;

        [Tooltip("Texture rotation (0, 90, 180, 270)")]
        [SerializeField] private int rotation;

        [Tooltip("Tint index for biome coloring (-1 = none)")]
        [SerializeField] private int tintIndex = -1;

        public string Texture
        {
            get { return texture; }
        }

        public Vector4 Uv
        {
            get { return uv; }
        }

        public CullFace CullFace
        {
            get { return cullFace; }
        }

        public int Rotation
        {
            get { return rotation; }
        }

        public int TintIndex
        {
            get { return tintIndex; }
        }
    }
}
