using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
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
}
