namespace Lithforge.Runtime.Content.Blocks
{
    /// <summary>
    /// Collision shape used by <see cref="Lithforge.Physics.VoxelCollider"/> for AABB resolution.
    /// Determines the physical bounds of a block for player and entity collision.
    /// </summary>
    public enum CollisionShapeType
    {
        /// <summary>Standard 1x1x1 solid cube — most blocks.</summary>
        FullCube = 0,
        /// <summary>No collision at all — air, torches, flowers.</summary>
        None = 1,
        /// <summary>Half-height block (bottom half only).</summary>
        Slab = 2,
        /// <summary>Stair-shaped — two AABBs (bottom slab + back half).</summary>
        Stairs = 3,
        /// <summary>Thin post with extended collision for connection detection.</summary>
        Fence = 4,
    }
}
