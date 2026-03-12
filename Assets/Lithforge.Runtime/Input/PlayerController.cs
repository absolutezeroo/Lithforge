using System;
using Lithforge.Physics;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lithforge.Runtime.Input
{
    /// <summary>
    /// Player movement controller with gravity and voxel collision.
    /// Uses VoxelCollider for AABB-vs-grid collision resolution.
    /// Position represents the player's feet (bottom-center of hitbox).
    /// Camera is a child object offset to eye height.
    /// Supports fly mode (F key) with scroll-wheel speed control
    /// and toggleable noclip (N key) while flying.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        private float _walkSpeed;
        private float _sprintSpeed;
        private float _gravity;
        private float _maxFallSpeed;
        private float _jumpSpeed;
        private float _playerHalfWidth;
        private float _playerHeight;

        private ChunkManager _chunkManager;
        private NativeStateRegistry _nativeStateRegistry;
        private GameLoop _gameLoop;
        private float _verticalSpeed;
        private bool _onGround;
        private Func<int3, bool> _isSolidDelegate;

        // Fly mode state
        private bool _flyMode;
        private bool _noclip;
        private float _flySpeed = 10f;

        private const float _minFlySpeed = 1f;
        private const float _maxFlySpeed = 150f;
        private const float _flySpeedScrollFactor = 1.2f;

        /// <summary>
        /// True if the player is standing on solid ground.
        /// </summary>
        public bool OnGround
        {
            get { return _onGround; }
        }

        /// <summary>
        /// True if fly mode is active.
        /// </summary>
        public bool IsFlying
        {
            get { return _flyMode; }
        }

        /// <summary>
        /// True if noclip is active (only meaningful while flying).
        /// </summary>
        public bool IsNoclip
        {
            get { return _noclip; }
        }

        /// <summary>
        /// Current fly speed in blocks per second.
        /// </summary>
        public float FlySpeed
        {
            get { return _flySpeed; }
        }

        /// <summary>
        /// Programmatically sets fly mode, noclip, and fly speed.
        /// Used by BenchmarkRunner for automated fly benchmarks.
        /// </summary>
        public void SetFlyMode(bool fly, bool noclip, float speed)
        {
            _flyMode = fly;
            _noclip = fly && noclip;
            _flySpeed = Mathf.Clamp(speed, _minFlySpeed, _maxFlySpeed);
            _verticalSpeed = 0f;

            if (fly)
            {
                _onGround = false;
            }
        }

        public void Initialize(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            GameLoop gameLoop,
            PhysicsSettings physics)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
            _gameLoop = gameLoop;
            _walkSpeed = physics.WalkSpeed;
            _sprintSpeed = physics.SprintSpeed;
            _gravity = physics.Gravity;
            _maxFallSpeed = physics.MaxFallSpeed;
            _jumpSpeed = physics.JumpVelocity;
            _playerHalfWidth = physics.PlayerHalfWidth;
            _playerHeight = physics.PlayerHeight;

            // Cache the delegate to avoid allocation each frame
            _isSolidDelegate = SolidBlockQuery.CreateDelegate(_chunkManager, _nativeStateRegistry);
        }

        private void Update()
        {
            if (_chunkManager == null)
            {
                return;
            }

            // Wait for spawn chunks to be generated before allowing movement
            if (_gameLoop != null && !_gameLoop.SpawnReady)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            // Toggle fly mode
            if (keyboard.fKey.wasPressedThisFrame)
            {
                _flyMode = !_flyMode;
                _verticalSpeed = 0f;

                if (_flyMode)
                {
                    _onGround = false;
                }
                else
                {
                    _noclip = false;
                }
            }

            // Toggle noclip (only while flying)
            if (keyboard.nKey.wasPressedThisFrame && _flyMode)
            {
                _noclip = !_noclip;
            }

            // Fly speed adjustment via scroll wheel
            if (_flyMode)
            {
                Mouse mouse = Mouse.current;

                if (mouse != null)
                {
                    float scroll = mouse.scroll.ReadValue().y;

                    if (scroll > 0.1f)
                    {
                        _flySpeed = Mathf.Clamp(_flySpeed * _flySpeedScrollFactor, _minFlySpeed, _maxFlySpeed);
                    }
                    else if (scroll < -0.1f)
                    {
                        _flySpeed = Mathf.Clamp(_flySpeed / _flySpeedScrollFactor, _minFlySpeed, _maxFlySpeed);
                    }
                }
            }

            float dt = Time.deltaTime;

            if (_flyMode)
            {
                UpdateFlyMode(keyboard, dt);
            }
            else
            {
                UpdateWalkMode(keyboard, dt);
            }
        }

        private void UpdateWalkMode(Keyboard keyboard, float dt)
        {
            // Compute horizontal displacement from input
            float3 displacement = ComputeHorizontalDisplacement(keyboard, dt, _walkSpeed, _sprintSpeed);

            // Apply gravity to vertical speed (blocks/sec, persistent across frames)
            _verticalSpeed += _gravity * dt;

            if (_verticalSpeed < _maxFallSpeed)
            {
                _verticalSpeed = _maxFallSpeed;
            }

            // Jump
            if (keyboard.spaceKey.wasPressedThisFrame && _onGround)
            {
                _verticalSpeed = _jumpSpeed;
                _onGround = false;
            }

            // Vertical displacement from accumulated vertical speed
            displacement.y = _verticalSpeed * dt;

            // Resolve collision
            float3 position = new float3(
                transform.position.x,
                transform.position.y,
                transform.position.z);

            CollisionResult result = VoxelCollider.Resolve(
                ref position,
                ref displacement,
                _playerHalfWidth,
                _playerHeight,
                _isSolidDelegate);

            _onGround = result.OnGround;

            // Reset vertical speed on ground or ceiling hit
            if (result.OnGround && _verticalSpeed < 0f)
            {
                _verticalSpeed = 0f;
            }

            if (result.HitCeiling && _verticalSpeed > 0f)
            {
                _verticalSpeed = 0f;
            }

            transform.position = new Vector3(position.x, position.y, position.z);
        }

        private void UpdateFlyMode(Keyboard keyboard, float dt)
        {
            // Horizontal movement (yaw-relative, same as walk)
            float3 displacement = ComputeHorizontalDisplacement(keyboard, dt, _flySpeed, _flySpeed);

            // Vertical: Space = ascend, Shift = descend
            if (keyboard.spaceKey.isPressed)
            {
                displacement.y += _flySpeed * dt;
            }

            if (keyboard.leftShiftKey.isPressed)
            {
                displacement.y -= _flySpeed * dt;
            }

            float3 position = new float3(
                transform.position.x,
                transform.position.y,
                transform.position.z);

            if (_noclip)
            {
                // No collision — apply displacement directly
                position += displacement;
            }
            else
            {
                // Fly with collision
                CollisionResult result = VoxelCollider.Resolve(
                    ref position,
                    ref displacement,
                    _playerHalfWidth,
                    _playerHeight,
                    _isSolidDelegate);

                _onGround = result.OnGround;
            }

            transform.position = new Vector3(position.x, position.y, position.z);
        }

        private float3 ComputeHorizontalDisplacement(Keyboard keyboard, float dt, float normalSpeed, float fastSpeed)
        {
            float speed = keyboard.leftShiftKey.isPressed ? fastSpeed : normalSpeed;

            // Horizontal movement relative to player facing direction (yaw only)
            float3 forward = new float3(transform.forward.x, 0f, transform.forward.z);
            forward = math.normalizesafe(forward);
            float3 right = new float3(transform.right.x, 0f, transform.right.z);
            right = math.normalizesafe(right);

            float3 moveDir = float3.zero;

            if (keyboard.wKey.isPressed)
            {
                moveDir += forward;
            }

            if (keyboard.sKey.isPressed)
            {
                moveDir -= forward;
            }

            if (keyboard.dKey.isPressed)
            {
                moveDir += right;
            }

            if (keyboard.aKey.isPressed)
            {
                moveDir -= right;
            }

            if (math.lengthsq(moveDir) > 0.001f)
            {
                moveDir = math.normalize(moveDir);
            }

            return new float3(moveDir.x * speed * dt, 0f, moveDir.z * speed * dt);
        }

    }
}
