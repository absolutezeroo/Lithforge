using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [System.Serializable]
    public sealed class ModelElement
    {
        [Tooltip("From corner position (0-16 range)")]
        [SerializeField] private Vector3 from;

        [Tooltip("To corner position (0-16 range)")]
        [SerializeField] private Vector3 to;

        [Header("Faces")]
        [SerializeField] private ModelFaceEntry north;
        [SerializeField] private ModelFaceEntry south;
        [SerializeField] private ModelFaceEntry east;
        [SerializeField] private ModelFaceEntry west;
        [SerializeField] private ModelFaceEntry up;
        [SerializeField] private ModelFaceEntry down;

        [Header("Rotation")]
        [SerializeField] private Vector3 rotationOrigin;
        [SerializeField] private ModelRotationAxis rotationAxis;
        [SerializeField] private float rotationAngle;
        [SerializeField] private bool rotationRescale;

        public Vector3 From
        {
            get { return from; }
        }

        public Vector3 To
        {
            get { return to; }
        }

        public ModelFaceEntry North
        {
            get { return north; }
        }

        public ModelFaceEntry South
        {
            get { return south; }
        }

        public ModelFaceEntry East
        {
            get { return east; }
        }

        public ModelFaceEntry West
        {
            get { return west; }
        }

        public ModelFaceEntry Up
        {
            get { return up; }
        }

        public ModelFaceEntry Down
        {
            get { return down; }
        }
    }
}
