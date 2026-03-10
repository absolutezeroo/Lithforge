using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    [CreateAssetMenu(fileName = "PhysicsSettings", menuName = "Lithforge/Settings/Physics", order = 3)]
    public sealed class PhysicsSettings : ScriptableObject
    {
        [Header("Player Movement")]
        [Tooltip("Walking speed in blocks per second")]
        [Min(0.1f)]
        [SerializeField] private float _walkSpeed = 4.317f;

        [Tooltip("Sprint speed in blocks per second")]
        [Min(0.1f)]
        [SerializeField] private float _sprintSpeed = 5.612f;

        [Tooltip("Jump launch velocity in blocks per second")]
        [Min(0.1f)]
        [SerializeField] private float _jumpVelocity = 8.5f;

        [Tooltip("Gravity acceleration in blocks per second squared (negative = downward)")]
        [SerializeField] private float _gravity = -28.0f;

        [Tooltip("Maximum downward velocity in blocks per second (negative)")]
        [SerializeField] private float _maxFallSpeed = -60.0f;

        [Header("Player Dimensions")]
        [Tooltip("Player eye height offset from feet position")]
        [Min(0.1f)]
        [SerializeField] private float _playerEyeHeight = 1.62f;

        [Tooltip("Half-width of player hitbox on X and Z axes")]
        [Min(0.01f)]
        [SerializeField] private float _playerHalfWidth = 0.3f;

        [Tooltip("Total height of player hitbox in blocks")]
        [Min(0.1f)]
        [SerializeField] private float _playerHeight = 1.8f;

        [Tooltip("Step-up height for walking over single-block ledges")]
        [Min(0f)]
        [SerializeField] private float _stepHeight = 0.6f;

        [Header("Interaction")]
        [Tooltip("Maximum reach distance for block interaction in blocks")]
        [Min(0.5f)]
        [SerializeField] private float _interactionRange = 5.0f;

        [Tooltip("Cooldown between block placements in seconds")]
        [Min(0f)]
        [SerializeField] private float _placeCooldownTime = 0.25f;

        [Header("Mining")]
        [Tooltip("Time multiplier when mining with bare hands (higher = slower)")]
        [Min(0.1f)]
        [SerializeField] private float _handMiningMultiplier = 5.0f;

        [Tooltip("Base time multiplier when mining with a tool (before tool speed modifier)")]
        [Min(0.1f)]
        [SerializeField] private float _toolMiningMultiplier = 1.5f;

        [Tooltip("Minimum block break time in seconds")]
        [Min(0.001f)]
        [SerializeField] private float _minBreakTime = 0.05f;

        [Header("Items")]
        [Tooltip("Default max stack size when no ItemEntry override exists")]
        [Min(1)]
        [SerializeField] private int _defaultMaxStackSize = 64;

        public float WalkSpeed
        {
            get { return _walkSpeed; }
        }

        public float SprintSpeed
        {
            get { return _sprintSpeed; }
        }

        public float JumpVelocity
        {
            get { return _jumpVelocity; }
        }

        public float Gravity
        {
            get { return _gravity; }
        }

        public float MaxFallSpeed
        {
            get { return _maxFallSpeed; }
        }

        public float PlayerEyeHeight
        {
            get { return _playerEyeHeight; }
        }

        public float PlayerHalfWidth
        {
            get { return _playerHalfWidth; }
        }

        public float PlayerHeight
        {
            get { return _playerHeight; }
        }

        public float StepHeight
        {
            get { return _stepHeight; }
        }

        public float InteractionRange
        {
            get { return _interactionRange; }
        }

        public float PlaceCooldownTime
        {
            get { return _placeCooldownTime; }
        }

        public float HandMiningMultiplier
        {
            get { return _handMiningMultiplier; }
        }

        public float ToolMiningMultiplier
        {
            get { return _toolMiningMultiplier; }
        }

        public float MinBreakTime
        {
            get { return _minBreakTime; }
        }

        public int DefaultMaxStackSize
        {
            get { return _defaultMaxStackSize; }
        }
    }
}
