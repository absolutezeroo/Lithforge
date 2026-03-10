using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [System.Serializable]
    public sealed class ModelElement
    {
        [Tooltip("From corner position (0-16 range)")]
        [SerializeField] private Vector3 _from;

        [Tooltip("To corner position (0-16 range)")]
        [SerializeField] private Vector3 _to;

        [Header("Faces")]
        [SerializeField] private ModelFaceEntry _north;
        [SerializeField] private ModelFaceEntry _south;
        [SerializeField] private ModelFaceEntry _east;
        [SerializeField] private ModelFaceEntry _west;
        [SerializeField] private ModelFaceEntry _up;
        [SerializeField] private ModelFaceEntry _down;

        [Header("Rotation")]
        [SerializeField] private Vector3 _rotationOrigin;
        [SerializeField] private ModelRotationAxis _rotationAxis;
        [SerializeField] private float _rotationAngle;
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
