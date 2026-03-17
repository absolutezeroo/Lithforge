using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Settings
{
    /// <summary>
    /// Player movement constants, hitbox dimensions, block interaction range, and mining speed multipliers.
    /// </summary>
    /// <remarks>
    /// Values are read every fixed tick by <c>PlayerPhysicsBody</c> and <c>BlockInteraction</c>.
    /// Loaded from <c>Resources/Settings/PhysicsSettings</c>.
    /// </remarks>
    [CreateAssetMenu(fileName = "PhysicsSettings", menuName = "Lithforge/Settings/Physics", order = 3)]
    public sealed class PhysicsSettings : ScriptableObject
    {
        /// <summary>Horizontal velocity while walking, in blocks per second.</summary>
        [Header("Player Movement")]
        [Tooltip("Walking speed in blocks per second")]
        [Min(0.1f)]
        [SerializeField] private float walkSpeed = 4.317f;

        /// <summary>Horizontal velocity while sprinting, in blocks per second.</summary>
        [Tooltip("Sprint speed in blocks per second")]
        [Min(0.1f)]
        [SerializeField] private float sprintSpeed = 5.612f;

        /// <summary>Instantaneous upward velocity applied when the player jumps, in blocks/s.</summary>
        [Tooltip("Jump launch velocity in blocks per second")]
        [Min(0.1f)]
        [SerializeField] private float jumpVelocity = 8.5f;

        /// <summary>Downward acceleration in blocks/s^2 (negative means toward ground).</summary>
        [Tooltip("Gravity acceleration in blocks per second squared (negative = downward)")]
        [SerializeField] private float gravity = -28.0f;

        /// <summary>Terminal velocity cap (negative) to prevent tunneling through terrain at high speeds.</summary>
        [Tooltip("Maximum downward velocity in blocks per second (negative)")]
        [SerializeField] private float maxFallSpeed = -60.0f;

        /// <summary>Vertical offset from the player's foot position to the camera, in blocks.</summary>
        [Header("Player Dimensions")]
        [Tooltip("Player eye height offset from feet position")]
        [Min(0.1f)]
        [SerializeField] private float playerEyeHeight = 1.62f;

        /// <summary>Half the side length of the square AABB cross-section used for collision.</summary>
        [Tooltip("Half-width of player hitbox on X and Z axes")]
        [Min(0.01f)]
        [SerializeField] private float playerHalfWidth = 0.3f;

        /// <summary>Full vertical extent of the player AABB, in blocks.</summary>
        [Tooltip("Total height of player hitbox in blocks")]
        [Min(0.1f)]
        [SerializeField] private float playerHeight = 1.8f;

        /// <summary>Maximum ledge height the player can automatically step up onto while walking.</summary>
        [Tooltip("Step-up height for walking over single-block ledges")]
        [Min(0f)]
        [SerializeField] private float stepHeight = 0.6f;

        /// <summary>How far from the camera the player can mine or place blocks, in block units.</summary>
        [Header("Interaction")]
        [Tooltip("Maximum reach distance for block interaction in blocks")]
        [Min(0.5f)]
        [SerializeField] private float interactionRange = 5.0f;

        /// <summary>Minimum seconds between successive block placements to prevent spam-clicking.</summary>
        [Tooltip("Cooldown between block placements in seconds")]
        [Min(0f)]
        [SerializeField] private float placeCooldownTime = 0.25f;

        /// <summary>Break-time multiplier applied when no tool is held (higher = slower mining).</summary>
        [Header("Mining")]
        [Tooltip("Time multiplier when mining with bare hands (higher = slower)")]
        [Min(0.1f)]
        [SerializeField] private float handMiningMultiplier = 5.0f;

        /// <summary>Base break-time multiplier when a tool is held, before the tool's own speed modifier.</summary>
        [Tooltip("Base time multiplier when mining with a tool (before tool speed modifier)")]
        [Min(0.1f)]
        [SerializeField] private float toolMiningMultiplier = 1.5f;

        /// <summary>Floor on computed break time so that even instant-break blocks require at least one tick.</summary>
        [Tooltip("Minimum block break time in seconds")]
        [Min(0.001f)]
        [SerializeField] private float minBreakTime = 0.05f;

        /// <summary>Horizontal swim acceleration in blocks/s^2 (Minecraft-equivalent: 0.6 at 30 TPS).</summary>
        [Header("Swimming")]
        [Tooltip("Horizontal movement acceleration in water (blocks/s^2)")]
        [Min(0f)]
        [SerializeField] private float swimAcceleration = 0.6f;

        /// <summary>Per-tick drag multiplier applied to velocity while swimming (0.8 = Minecraft).</summary>
        [Tooltip("Velocity drag per tick in water (0.8 = Minecraft default)")]
        [Range(0f, 1f)]
        [SerializeField] private float swimDrag = 0.8f;

        /// <summary>Downward acceleration in water, in blocks/s^2 (reduced from land gravity).</summary>
        [Tooltip("Gravity in water (blocks/s^2, negative = down)")]
        [SerializeField] private float swimGravity = -0.6f;

        /// <summary>Upward acceleration when holding jump while swimming, in blocks/s^2.</summary>
        [Tooltip("Swim-up acceleration when holding jump (blocks/s^2)")]
        [Min(0f)]
        [SerializeField] private float swimUpSpeed = 1.2f;

        /// <summary>Stack limit used for items whose <c>ItemDefinition</c> does not specify a custom cap.</summary>
        [Header("Items")]
        [Tooltip("Default max stack size when no ItemEntry override exists")]
        [Min(1)]
        [SerializeField] private int defaultMaxStackSize = 64;

        /// <inheritdoc cref="walkSpeed"/>
        public float WalkSpeed
        {
            get { return walkSpeed; }
        }

        /// <inheritdoc cref="sprintSpeed"/>
        public float SprintSpeed
        {
            get { return sprintSpeed; }
        }

        /// <inheritdoc cref="jumpVelocity"/>
        public float JumpVelocity
        {
            get { return jumpVelocity; }
        }

        /// <inheritdoc cref="gravity"/>
        public float Gravity
        {
            get { return gravity; }
        }

        /// <inheritdoc cref="maxFallSpeed"/>
        public float MaxFallSpeed
        {
            get { return maxFallSpeed; }
        }

        /// <inheritdoc cref="playerEyeHeight"/>
        public float PlayerEyeHeight
        {
            get { return playerEyeHeight; }
        }

        /// <inheritdoc cref="playerHalfWidth"/>
        public float PlayerHalfWidth
        {
            get { return playerHalfWidth; }
        }

        /// <inheritdoc cref="playerHeight"/>
        public float PlayerHeight
        {
            get { return playerHeight; }
        }

        /// <inheritdoc cref="stepHeight"/>
        public float StepHeight
        {
            get { return stepHeight; }
        }

        /// <inheritdoc cref="interactionRange"/>
        public float InteractionRange
        {
            get { return interactionRange; }
        }

        /// <inheritdoc cref="placeCooldownTime"/>
        public float PlaceCooldownTime
        {
            get { return placeCooldownTime; }
        }

        /// <inheritdoc cref="handMiningMultiplier"/>
        public float HandMiningMultiplier
        {
            get { return handMiningMultiplier; }
        }

        /// <inheritdoc cref="toolMiningMultiplier"/>
        public float ToolMiningMultiplier
        {
            get { return toolMiningMultiplier; }
        }

        /// <inheritdoc cref="minBreakTime"/>
        public float MinBreakTime
        {
            get { return minBreakTime; }
        }

        /// <inheritdoc cref="swimAcceleration"/>
        public float SwimAcceleration
        {
            get { return swimAcceleration; }
        }

        /// <inheritdoc cref="swimDrag"/>
        public float SwimDrag
        {
            get { return swimDrag; }
        }

        /// <inheritdoc cref="swimGravity"/>
        public float SwimGravity
        {
            get { return swimGravity; }
        }

        /// <inheritdoc cref="swimUpSpeed"/>
        public float SwimUpSpeed
        {
            get { return swimUpSpeed; }
        }

        /// <inheritdoc cref="defaultMaxStackSize"/>
        public int DefaultMaxStackSize
        {
            get { return defaultMaxStackSize; }
        }
    }
}
