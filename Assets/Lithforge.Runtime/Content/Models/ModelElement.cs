using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Models
{
    [System.Serializable]
    public sealed class ModelElement
    {
        [FormerlySerializedAs("_from"),Tooltip("From corner position (0-16 range)")]
        [SerializeField] private Vector3 from;

        [FormerlySerializedAs("_to"),Tooltip("To corner position (0-16 range)")]
        [SerializeField] private Vector3 to;

        [FormerlySerializedAs("_north"),Header("Faces")]
        [SerializeField] private ModelFaceEntry north;

        [FormerlySerializedAs("_south"),SerializeField] private ModelFaceEntry south;

        [FormerlySerializedAs("_east"),SerializeField] private ModelFaceEntry east;

        [FormerlySerializedAs("_west"),SerializeField] private ModelFaceEntry west;

        [FormerlySerializedAs("_up"),SerializeField] private ModelFaceEntry up;

        [FormerlySerializedAs("_down"),SerializeField] private ModelFaceEntry down;

        [FormerlySerializedAs("_rotationOrigin"),Header("Rotation")]
        [SerializeField] private Vector3 rotationOrigin;

        [FormerlySerializedAs("_rotationAxis"),SerializeField] private ModelRotationAxis rotationAxis;

        [FormerlySerializedAs("_rotationAngle"),SerializeField] private float rotationAngle;

        [FormerlySerializedAs("_rotationRescale"),SerializeField] private bool rotationRescale;

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
