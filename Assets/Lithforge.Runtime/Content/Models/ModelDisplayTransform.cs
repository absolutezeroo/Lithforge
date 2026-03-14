using System;
using UnityEngine;

namespace Lithforge.Runtime.Content.Models
{
    /// <summary>
    /// Display transform for a model in a specific view context (e.g., firstperson_righthand).
    /// Matches Minecraft's display transform format: rotation, translation, scale.
    /// Translation units are 1/16 of a block. Rotation in degrees [X, Y, Z].
    /// Minecraft transform order: Translate → RotateY → RotateX → RotateZ → Scale.
    /// </summary>
    [Serializable]
    public sealed class ModelDisplayTransform
    {
        [Tooltip("Whether this display transform has been explicitly set.")]
        [SerializeField] private bool _hasValue;

        [Tooltip("Rotation in degrees [X, Y, Z].")]
        [SerializeField] private Vector3 _rotation;

        [Tooltip("Translation in 1/16 block units. Clamped to [-80, 80].")]
        [SerializeField] private Vector3 _translation;

        [Tooltip("Scale per axis. Capped at 4.")]
        [SerializeField] private Vector3 _scale = Vector3.one;

        public bool HasValue
        {
            get { return _hasValue; }
        }

        public Vector3 Rotation
        {
            get { return _rotation; }
        }

        public Vector3 Translation
        {
            get { return _translation; }
        }

        public Vector3 Scale
        {
            get { return _scale; }
        }
    }
}
