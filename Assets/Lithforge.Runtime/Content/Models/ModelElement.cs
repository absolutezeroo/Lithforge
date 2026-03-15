using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Models
{
    /// <summary>
    /// An axis-aligned cuboid within a <see cref="BlockModel"/>, defined by two corner
    /// positions in 0-16 voxel-local space. Each of the six faces can carry its own
    /// texture, UV region, cull rule, and tint. An optional rotation pivots the cuboid
    /// around an arbitrary origin before meshing.
    /// </summary>
    /// <remarks>
    /// Coordinates use the Minecraft convention where one block spans [0, 16] on each
    /// axis. If a child model defines any elements, they replace the parent's elements
    /// entirely rather than merging.
    /// </remarks>
    [System.Serializable]
    public sealed class ModelElement
    {
        /// <summary>
        /// Minimum corner of the cuboid in 0-16 voxel-local coordinates.
        /// </summary>
        [FormerlySerializedAs("_from"),Tooltip("From corner position (0-16 range)")]
        [SerializeField] private Vector3 from;

        /// <summary>
        /// Maximum corner of the cuboid in 0-16 voxel-local coordinates.
        /// </summary>
        [FormerlySerializedAs("_to"),Tooltip("To corner position (0-16 range)")]
        [SerializeField] private Vector3 to;

        /// <summary>Face data for the +Z side of this cuboid.</summary>
        [FormerlySerializedAs("_north"),Header("Faces")]
        [SerializeField] private ModelFaceEntry north;

        /// <summary>Face data for the -Z side of this cuboid.</summary>
        [FormerlySerializedAs("_south"),SerializeField] private ModelFaceEntry south;

        /// <summary>Face data for the +X side of this cuboid.</summary>
        [FormerlySerializedAs("_east"),SerializeField] private ModelFaceEntry east;

        /// <summary>Face data for the -X side of this cuboid.</summary>
        [FormerlySerializedAs("_west"),SerializeField] private ModelFaceEntry west;

        /// <summary>Face data for the +Y side of this cuboid.</summary>
        [FormerlySerializedAs("_up"),SerializeField] private ModelFaceEntry up;

        /// <summary>Face data for the -Y side of this cuboid.</summary>
        [FormerlySerializedAs("_down"),SerializeField] private ModelFaceEntry down;

        /// <summary>
        /// Pivot point for the element rotation, in 0-16 voxel-local coordinates.
        /// </summary>
        [FormerlySerializedAs("_rotationOrigin"),Header("Rotation")]
        [SerializeField] private Vector3 rotationOrigin;

        /// <summary>
        /// Which axis the element rotates around (X, Y, or Z).
        /// </summary>
        [FormerlySerializedAs("_rotationAxis"),SerializeField] private ModelRotationAxis rotationAxis;

        /// <summary>
        /// Rotation angle in degrees. Valid values are -45, -22.5, 0, 22.5, and 45.
        /// </summary>
        [FormerlySerializedAs("_rotationAngle"),SerializeField] private float rotationAngle;

        /// <summary>
        /// When true, the cuboid is rescaled after rotation so it still fills the
        /// original axis-aligned bounding box. Prevents diagonal elements from
        /// poking outside a full block.
        /// </summary>
        [FormerlySerializedAs("_rotationRescale"),SerializeField] private bool rotationRescale;

        /// <summary>
        /// Minimum corner of the cuboid in 0-16 voxel-local coordinates.
        /// </summary>
        public Vector3 From
        {
            get { return from; }
        }

        /// <summary>
        /// Maximum corner of the cuboid in 0-16 voxel-local coordinates.
        /// </summary>
        public Vector3 To
        {
            get { return to; }
        }

        /// <summary>Face data for the +Z (north) side.</summary>
        public ModelFaceEntry North
        {
            get { return north; }
        }

        /// <summary>Face data for the -Z (south) side.</summary>
        public ModelFaceEntry South
        {
            get { return south; }
        }

        /// <summary>Face data for the +X (east) side.</summary>
        public ModelFaceEntry East
        {
            get { return east; }
        }

        /// <summary>Face data for the -X (west) side.</summary>
        public ModelFaceEntry West
        {
            get { return west; }
        }

        /// <summary>Face data for the +Y (up) side.</summary>
        public ModelFaceEntry Up
        {
            get { return up; }
        }

        /// <summary>Face data for the -Y (down) side.</summary>
        public ModelFaceEntry Down
        {
            get { return down; }
        }
    }
}
