namespace Lithforge.Runtime.Content.Models
{
    /// <summary>
    /// Axis around which a <see cref="ModelElement"/> is rotated relative to its
    /// <c>rotationOrigin</c> pivot point.
    /// </summary>
    public enum ModelRotationAxis
    {
        /// <summary>Rotate around the X axis (pitch).</summary>
        X = 0,

        /// <summary>Rotate around the Y axis (yaw).</summary>
        Y = 1,

        /// <summary>Rotate around the Z axis (roll).</summary>
        Z = 2,
    }
}
