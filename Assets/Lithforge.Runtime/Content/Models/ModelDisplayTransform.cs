using System;
using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("_hasValue"),Tooltip("Whether this display transform has been explicitly set.")]
        [SerializeField] private bool hasValue;

        [FormerlySerializedAs("_rotation"),Tooltip("Rotation in degrees [X, Y, Z].")]
        [SerializeField] private Vector3 rotation;

        [FormerlySerializedAs("_translation"),Tooltip("Translation in 1/16 block units. Clamped to [-80, 80].")]
        [SerializeField] private Vector3 translation;

        [FormerlySerializedAs("_scale"),Tooltip("Scale per axis. Capped at 4.")]
        [SerializeField] private Vector3 scale = Vector3.one;

        public bool HasValue
        {
            get { return hasValue; }
        }

        public Vector3 Rotation
        {
            get { return rotation; }
        }

        public Vector3 Translation
        {
            get { return translation; }
        }

        public Vector3 Scale
        {
            get { return scale; }
        }
    }
}
