using Lithforge.Physics;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Input;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Mathematics;
using UnityEngine;

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
        /// When true, tick methods are skipped and GameLoop does not interpolate position.
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
        /// Called once per fixed tick by GameLoop with the current input snapshot.
        /// All input reads come from the snapshot — no direct Keyboard/Mouse access.
        /// Forward/right vectors are computed from <see cref="InputSnapshot.Yaw"/>
        /// using trig, removing the dependency on Transform.forward.
        /// </summary>
        public void TickWithSnapshot(float tickDt, in InputSnapshot snapshot)
        {
            if (ExternallyControlled)
            {
                return;
            }

            if (!_spawnReady)
            {
                return;
            }

            // Toggle fly mode (edge input from snapshot)
            if (snapshot.FlyTogglePressed)
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

            // Toggle noclip (edge input from snapshot)
            if (snapshot.NoclipTogglePressed && _flyMode)
            {
                _noclip = !_noclip;
            }

            // Fly speed adjustment via scroll (accumulated in snapshot)
            if (_flyMode && snapshot.ScrollDelta != 0)
            {
                if (snapshot.ScrollDelta > 0)
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
                TickFlyFromSnapshot(in snapshot, tickDt);
            }
            else
            {
                TickWalkFromSnapshot(in snapshot, tickDt);
            }

            // Write position back to transform so CameraController (child) tracks correctly.
            // LateUpdate will overwrite with the interpolated position for rendering.
            _playerTransform.position = new Vector3(
                _currentPosition.x, _currentPosition.y, _currentPosition.z);
        }

        private void TickWalkFromSnapshot(in InputSnapshot snapshot, float dt)
        {
            float3 displacement = ComputeHorizontalFromSnapshot(
                in snapshot, dt, _walkSpeed, _sprintSpeed);

            _verticalSpeed += _gravity * dt;

            if (_verticalSpeed < _maxFallSpeed)
            {
                _verticalSpeed = _maxFallSpeed;
            }

            // Jump — edge input from snapshot
            if (snapshot.JumpPressed && _onGround)
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

        private void TickFlyFromSnapshot(in InputSnapshot snapshot, float dt)
        {
            float3 displacement = ComputeHorizontalFromSnapshot(
                in snapshot, dt, _flySpeed, _flySpeed);

            if (snapshot.JumpHeld)
            {
                displacement.y += _flySpeed * dt;
            }

            if (snapshot.Sprint)
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

        /// <summary>
        /// Computes horizontal movement from InputSnapshot fields and yaw.
        /// Forward/right are derived from snapshot.Yaw using trig (no Transform read).
        /// </summary>
        private float3 ComputeHorizontalFromSnapshot(
            in InputSnapshot snapshot, float dt, float normalSpeed, float fastSpeed)
        {
            _isSprinting = snapshot.Sprint && !_flyMode;
            float speed = snapshot.Sprint ? fastSpeed : normalSpeed;

            // Derive forward/right from yaw (degrees) — no Transform.forward dependency
            float yawRad = math.radians(snapshot.Yaw);
            float3 forward = new float3(math.sin(yawRad), 0f, math.cos(yawRad));
            float3 right = new float3(math.cos(yawRad), 0f, -math.sin(yawRad));

            float3 moveDir = float3.zero;

            if (snapshot.MoveForward)
            {
                moveDir += forward;
            }

            if (snapshot.MoveBack)
            {
                moveDir -= forward;
            }

            if (snapshot.MoveRight)
            {
                moveDir += right;
            }

            if (snapshot.MoveLeft)
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
