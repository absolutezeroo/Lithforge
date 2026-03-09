namespace Lithforge.Physics
{
    /// <summary>
    /// Physics constants for player movement and collision.
    /// </summary>
    public static class PhysicsConstants
    {
        /// <summary>
        /// Gravity acceleration in blocks per second squared (negative = downward).
        /// </summary>
        public const float Gravity = -28f;

        /// <summary>
        /// Maximum downward velocity in blocks per second.
        /// </summary>
        public const float MaxFallSpeed = -60f;

        /// <summary>
        /// Initial upward velocity when jumping, in blocks per second.
        /// Yields approximately 1.25 block jump height.
        /// </summary>
        public const float JumpSpeed = 8.5f;

        /// <summary>
        /// Half-width of the player hitbox on X and Z axes.
        /// Full width = 0.6 blocks (matching Minecraft).
        /// </summary>
        public const float PlayerHalfWidth = 0.3f;

        /// <summary>
        /// Total height of the player hitbox in blocks.
        /// </summary>
        public const float PlayerHeight = 1.8f;

        /// <summary>
        /// Eye height offset from the player's feet position.
        /// </summary>
        public const float PlayerEyeHeight = 1.62f;

        /// <summary>
        /// Walking speed in blocks per second.
        /// </summary>
        public const float WalkSpeed = 4.317f;

        /// <summary>
        /// Sprint speed in blocks per second.
        /// </summary>
        public const float SprintSpeed = 5.612f;

        /// <summary>
        /// Maximum reach distance for block interaction in blocks.
        /// </summary>
        public const float InteractionRange = 5f;

        /// <summary>
        /// Small epsilon for ground detection tolerance.
        /// </summary>
        public const float GroundEpsilon = 0.001f;

        /// <summary>
        /// Step-up height for walking over single-block ledges.
        /// </summary>
        public const float StepHeight = 0.6f;
    }
}
