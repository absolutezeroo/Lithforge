using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Models
{
    [System.Serializable]
    public sealed class ModelFaceEntry
    {
        [FormerlySerializedAs("texture")]
        [Tooltip("Texture variable reference (e.g. '#all', '#side')")]
        [SerializeField] private string _texture;

        [FormerlySerializedAs("uv")]
        [Tooltip("UV coordinates [u1, v1, u2, v2]")]
        [SerializeField] private Vector4 _uv;

        [FormerlySerializedAs("cullFace")]
        [Tooltip("Face culling direction")]
        [SerializeField] private CullFace _cullFace = CullFace.None;

        [FormerlySerializedAs("rotation")]
        [Tooltip("Texture rotation (0, 90, 180, 270)")]
        [SerializeField] private int _rotation;

        [FormerlySerializedAs("tintIndex")]
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
}
