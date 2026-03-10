using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    [CreateAssetMenu(fileName = "PhysicsSettings", menuName = "Lithforge/Settings/Physics", order = 3)]
    public sealed class PhysicsSettings : ScriptableObject
    {
        [Header("Player")]
        [Tooltip("Player eye height")]
        [Min(0.1f)]
        [SerializeField] private float _playerEyeHeight = 1.62f;

        [Tooltip("Player movement speed")]
        [Min(0.1f)]
        [SerializeField] private float _playerSpeed = 4.317f;

        [Tooltip("Player jump velocity")]
        [Min(0.1f)]
        [SerializeField] private float _jumpVelocity = 8.0f;

        [Tooltip("Gravity acceleration")]
        [SerializeField] private float _gravity = -28.0f;

        public float PlayerEyeHeight
        {
            get { return _playerEyeHeight; }
        }

        public float PlayerSpeed
        {
            get { return _playerSpeed; }
        }

        public float JumpVelocity
        {
            get { return _jumpVelocity; }
        }

        public float Gravity
        {
            get { return _gravity; }
        }
    }
}
