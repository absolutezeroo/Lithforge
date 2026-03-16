using Lithforge.Physics;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Input;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lithforge.Runtime.Tick
{
    /// <summary>
    /// Pure simulation body for the player: movement, gravity, collision, fly mode.
    /// Operates entirely at fixed tick rate — no reference to Time.deltaTime.
    ///
    /// Position convention: PreviousPosition and CurrentPosition represent the
    /// player's feet (bottom-center of AABB), as float3 in world space.
    /// LateUpdate interpolation blends between them using TickAccumulator.Alpha.
    /// </summary>
    public sealed class PlayerPhysicsBody
    {
        // Physics settings
        private readonly float _walkSpeed;
        private readonly float _sprintSpeed;
        private readonly float _gravity;
        private readonly float _maxFallSpeed;
        private readonly float _jumpSpeed;
        private readonly float _playerHalfWidth;
        private readonly float _playerHeight;

        // World access
        private readonly ChunkManager _chunkManager;
        private readonly NativeStateRegistry _nativeStateRegistry;

        // Player transform — read for yaw direction
        private readonly Transform _playerTransform;

        // Fly mode constants
        private const float MinFlySpeed = 1f;
        private const float MaxFlySpeed = 150f;
        private const float FlySpeedScrollFactor = 1.2f;

        // Interpolation positions — read by GameLoop.LateUpdate()
        private float3 _previousPosition;
        private float3 _currentPosition;

        // Physics state
        private float _verticalSpeed;
        private bool _onGround;
        private bool _isSprinting;

        // Fly mode state
        private bool _flyMode;
        private bool _noclip;
        private float _flySpeed = 10f;

        // Spawn guard
        private bool _spawnReady;

        public float3 PreviousPosition
        {
            get { return _previousPosition; }
        }

        public float3 CurrentPosition
        {
            get { return _currentPosition; }
        }

        public bool OnGround
        {
            get { return _onGround; }
        }

        public bool IsFlying
        {
            get { return _flyMode; }
        }

        public bool IsNoclip
        {
            get { return _noclip; }
        }

        public bool IsSprinting
        {
            get { return _isSprinting; }
        }

        public float FlySpeed
        {
            get { return _flySpeed; }
        }

        public bool SpawnReady
        {
            get { return _spawnReady; }
            set { _spawnReady = value; }
        }

        /// <summary>
        /// When true, TickWithLatch is skipped and GameLoop does not interpolate position.
        /// Used by BenchmarkRunner to control the player transform directly at frame rate.
        /// Call Teleport() after clearing this to sync position back to the physics body.
        /// </summary>
        public bool ExternallyControlled { get; set; }

        public PlayerPhysicsBody(
            float3 startPosition,
            Transform playerTransform,
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            PhysicsSettings physics)
        {
            _currentPosition = startPosition;
            _previousPosition = startPosition;
            _playerTransform = playerTransform;
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
            _walkSpeed = physics.WalkSpeed;
            _sprintSpeed = physics.SprintSpeed;
            _gravity = physics.Gravity;
            _maxFallSpeed = physics.MaxFallSpeed;
            _jumpSpeed = physics.JumpVelocity;
            _playerHalfWidth = physics.PlayerHalfWidth;
            _playerHeight = physics.PlayerHeight;
        }

        /// <summary>
        /// Programmatic fly mode setter, used by BenchmarkRunner.
        /// </summary>
        public void SetFlyMode(bool fly, bool noclip, float speed)
        {
            _flyMode = fly;
            _noclip = fly && noclip;
            _flySpeed = math.clamp(speed, MinFlySpeed, MaxFlySpeed);
            _verticalSpeed = 0f;

            if (fly)
            {
                _onGround = false;
            }
        }

        /// <summary>
        /// Sets the current position directly, used during teleport (spawn, benchmark).
        /// Also sets previous position to avoid interpolation jump.
        /// </summary>
        public void Teleport(float3 position)
        {
            _currentPosition = position;
            _previousPosition = position;
            _verticalSpeed = 0f;
        }

        /// <summary>
        /// Called once per fixed tick by GameLoop with the current latch snapshot.
        /// Advances physics by tickDt.
        /// </summary>
        public void TickWithLatch(float tickDt, in InputLatchSnapshot latch)
        {
            if (ExternallyControlled)
            {
                return;
            }

            if (!_spawnReady)
            {
                return;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            // Toggle fly mode (edge input from latch)
            if (latch.FlyTogglePressed)
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

            // Toggle noclip (edge input from latch)
            if (latch.NoclipTogglePressed && _flyMode)
            {
                _noclip = !_noclip;
            }

            // Fly speed adjustment via scroll (accumulated in latch)
            if (_flyMode && latch.ScrollDelta != 0)
            {
                if (latch.ScrollDelta > 0)
                {
                    _flySpeed = math.clamp(
                        _flySpeed * FlySpeedScrollFactor, MinFlySpeed, MaxFlySpeed);
                }
                else
                {
                    _flySpeed = math.clamp(
                        _flySpeed / FlySpeedScrollFactor, MinFlySpeed, MaxFlySpeed);
                }
            }

            // Snapshot previous position for interpolation
            _previousPosition = _currentPosition;

            if (_flyMode)
            {
                TickFly(keyboard, tickDt);
            }
            else
            {
                TickWalk(keyboard, tickDt, in latch);
            }

            // Write position back to transform so CameraController (child) tracks correctly.
            // LateUpdate will overwrite with the interpolated position for rendering.
            _playerTransform.position = new Vector3(
                _currentPosition.x, _currentPosition.y, _currentPosition.z);
        }

        private void TickWalk(Keyboard keyboard, float dt, in InputLatchSnapshot latch)
        {
            float3 displacement = ComputeHorizontalDisplacement(keyboard, dt, _walkSpeed, _sprintSpeed);

            _verticalSpeed += _gravity * dt;

            if (_verticalSpeed < _maxFallSpeed)
            {
                _verticalSpeed = _maxFallSpeed;
            }

            // Jump — edge input from latch
            if (latch.JumpPressed && _onGround)
            {
                _verticalSpeed = _jumpSpeed;
                _onGround = false;
            }

            displacement.y = _verticalSpeed * dt;

            SolidBlockQuery query = SolidBlockHelper.Build(
                _currentPosition, displacement, _playerHalfWidth, _playerHeight,
                _chunkManager, _nativeStateRegistry);

            CollisionResult result = VoxelCollider.Resolve(
                ref _currentPosition, ref displacement,
                _playerHalfWidth, _playerHeight, query);

            query.SolidMap.Dispose();

            _onGround = result.OnGround;

            if (result.OnGround && _verticalSpeed < 0f)
            {
                _verticalSpeed = 0f;
            }

            if (result.HitCeiling && _verticalSpeed > 0f)
            {
                _verticalSpeed = 0f;
            }
        }

        private void TickFly(Keyboard keyboard, float dt)
        {
            float3 displacement = ComputeHorizontalDisplacement(keyboard, dt, _flySpeed, _flySpeed);

            if (keyboard.spaceKey.isPressed)
            {
                displacement.y += _flySpeed * dt;
            }

            if (keyboard.leftShiftKey.isPressed)
            {
                displacement.y -= _flySpeed * dt;
            }

            if (_noclip)
            {
                _currentPosition += displacement;
            }
            else
            {
                SolidBlockQuery query = SolidBlockHelper.Build(
                    _currentPosition, displacement, _playerHalfWidth, _playerHeight,
                    _chunkManager, _nativeStateRegistry);

                CollisionResult result = VoxelCollider.Resolve(
                    ref _currentPosition, ref displacement,
                    _playerHalfWidth, _playerHeight, query);

                query.SolidMap.Dispose();
                _onGround = result.OnGround;
            }
        }

        private float3 ComputeHorizontalDisplacement(
            Keyboard keyboard, float dt, float normalSpeed, float fastSpeed)
        {
            _isSprinting = keyboard.leftShiftKey.isPressed && !_flyMode;
            float speed = keyboard.leftShiftKey.isPressed ? fastSpeed : normalSpeed;

            // Read yaw from the player transform (set by CameraController at frame rate)
            float3 forward = new float3(
                _playerTransform.forward.x, 0f, _playerTransform.forward.z);
            forward = math.normalizesafe(forward);
            float3 right = new float3(
                _playerTransform.right.x, 0f, _playerTransform.right.z);
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
