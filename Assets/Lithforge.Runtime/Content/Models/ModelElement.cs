using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Models
{
    [System.Serializable]
    public sealed class ModelElement
    {
        [FormerlySerializedAs("from")]
        [Tooltip("From corner position (0-16 range)")]
        [SerializeField] private Vector3 _from;

        [FormerlySerializedAs("to")]
        [Tooltip("To corner position (0-16 range)")]
        [SerializeField] private Vector3 _to;

        [FormerlySerializedAs("north")]
        [Header("Faces")]
        [SerializeField] private ModelFaceEntry _north;

        [FormerlySerializedAs("south")]
        [SerializeField] private ModelFaceEntry _south;

        [FormerlySerializedAs("east")]
        [SerializeField] private ModelFaceEntry _east;

        [FormerlySerializedAs("west")]
        [SerializeField] private ModelFaceEntry _west;

        [FormerlySerializedAs("up")]
        [SerializeField] private ModelFaceEntry _up;

        [FormerlySerializedAs("down")]
        [SerializeField] private ModelFaceEntry _down;

        [FormerlySerializedAs("rotationOrigin")]
        [Header("Rotation")]
        [SerializeField] private Vector3 _rotationOrigin;

        [FormerlySerializedAs("rotationAxis")]
        [SerializeField] private ModelRotationAxis _rotationAxis;

        [FormerlySerializedAs("rotationAngle")]
        [SerializeField] private float _rotationAngle;

        [FormerlySerializedAs("rotationRescale")]
        [SerializeField] private bool _rotationRescale;

        public Vector3 From
        {
            get { return _from; }
        }

        public Vector3 To
        {
            get { return _to; }
        }

        public ModelFaceEntry North
        {
            get { return _north; }
        }

        public ModelFaceEntry South
        {
            get { return _south; }
        }

        public ModelFaceEntry East
        {
            get { return _east; }
        }

        public ModelFaceEntry West
        {
            get { return _west; }
        }

        public ModelFaceEntry Up
        {
            get { return _up; }
        }

        public ModelFaceEntry Down
        {
            get { return _down; }
        }
    }
}
