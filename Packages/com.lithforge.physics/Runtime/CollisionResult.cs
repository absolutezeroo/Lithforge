namespace Lithforge.Physics
{
    /// <summary>
    /// Result of AABB vs voxel grid collision resolution.
    /// </summary>
    public struct CollisionResult
    {
        /// <summary>
        /// True if the entity is resting on a solid surface below.
        /// </summary>
        public bool OnGround;

        /// <summary>
        /// True if the entity hit a ceiling above.
        /// </summary>
        public bool HitCeiling;

        /// <summary>
        /// True if the entity hit a wall on the X or Z axis.
        /// </summary>
        public bool HitWall;
    }
}
