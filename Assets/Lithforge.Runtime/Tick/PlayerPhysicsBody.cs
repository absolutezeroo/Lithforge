using System;

using Lithforge.Physics;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Input;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

namespace Lithforge.Runtime.Tick
{
    /// <summary>
    ///     Pure simulation body for the player: movement, gravity, collision, fly mode, swimming.
    ///     Operates entirely at fixed tick rate — no reference to Time.deltaTime.
    ///     Position convention: PreviousPosition and CurrentPosition represent the
    ///     player's feet (bottom-center of AABB), as float3 in world space.
    ///     LateUpdate interpolation blends between them using TickAccumulator.Alpha.
    /// </summary>
    public sealed class PlayerPhysicsBody
    {
        // Fly mode constants
        private const float MinFlySpeed = 1f;
        private const float MaxFlySpeed = 150f;
        private const float FlySpeedScrollFactor = 1.2f;

        // World access
        private readonly IChunkDataReader _chunkDataReader;
        private readonly float _gravity;
        private readonly float _jumpSpeed;
        private readonly float _maxFallSpeed;
        private readonly NativeStateRegistry _nativeStateRegistry;
        private readonly float _playerEyeHeight;
        private readonly float _playerHalfWidth;
        private readonly float _playerHeight;
        private readonly float _sprintSpeed;

        // Swim settings
        private readonly float _swimAcceleration;
        private readonly float _swimDrag;
        private readonly float _swimGravity;
        private readonly float _swimUpSpeed;
        // Physics settings
        private readonly float _walkSpeed;
        private float3 _currentPosition;

        // Fly mode state
        private float _horizontalSpeedX;
        private float _horizontalSpeedZ;

        // Swim state
        private float _pitch;

        // Interpolation positions — read by GameLoop.LateUpdate()

        // Spawn guard

        /// <summary>
        ///     Optional collision state override for unconfirmed block predictions.
        ///     When set, collision resolves against server-confirmed state instead of
        ///     optimistically-applied predictions, preventing cascading mispredictions.
        /// </summary>
        private Func<int3, StateId?> _collisionOverride;

        // Physics state
        private float _verticalSpeed;
        private bool _wasInWater;
        private float _yaw;

        public PlayerPhysicsBody(
            float3 startPosition,
            IChunkDataReader chunkDataReader,
            NativeStateRegistry nativeStateRegistry,
            PhysicsSettings physics)
        {
            _currentPosition = startPosition;
            PreviousPosition = startPosition;
            _chunkDataReader = chunkDataReader;
            _nativeStateRegistry = nativeStateRegistry;
            _walkSpeed = physics.WalkSpeed;
            _sprintSpeed = physics.SprintSpeed;
            _gravity = physics.Gravity;
            _maxFallSpeed = physics.MaxFallSpeed;
            _jumpSpeed = physics.JumpVelocity;
            _playerHalfWidth = physics.PlayerHalfWidth;
            _playerHeight = physics.PlayerHeight;
            _playerEyeHeight = physics.PlayerEyeHeight;
            _swimAcceleration = physics.SwimAcceleration;
            _swimDrag = physics.SwimDrag;
            _swimGravity = physics.SwimGravity;
            _swimUpSpeed = physics.SwimUpSpeed;
        }

        public float3 PreviousPosition { get; private set; }

        public float3 CurrentPosition
        {
            get { return _currentPosition; }
        }

        public bool OnGround { get; private set; }

        public bool IsFlying { get; private set; }

        public bool IsNoclip { get; private set; }

        public bool IsSprinting { get; private set; }

        public bool IsInWater { get; private set; }

        public bool IsSubmerged { get; private set; }

        public float FlySpeed { get; private set; } = 10f;

        public bool SpawnReady { get; set; }

        /// <summary>
        ///     When true, tick methods are skipped and GameLoop does not interpolate position.
        ///     Used by BenchmarkRunner to control the player transform directly at frame rate.
        ///     Call Teleport() after clearing this to sync position back to the physics body.
        /// </summary>
        public bool ExternallyControlled { get; set; }

        /// <summary>
        ///     Programmatic fly mode setter, used by BenchmarkRunner.
        /// </summary>
        public void SetFlyMode(bool fly, bool noclip, float speed)
        {
            IsFlying = fly;
            IsNoclip = fly && noclip;
            FlySpeed = math.clamp(speed, MinFlySpeed, MaxFlySpeed);
            _verticalSpeed = 0f;

            if (fly)
            {
                OnGround = false;
            }
        }

        /// <summary>
        ///     Sets the collision state override delegate for unconfirmed block predictions.
        ///     When a coordinate has an unconfirmed prediction, the delegate returns the
        ///     original (pre-prediction) StateId so collision resolves against server-confirmed
        ///     state. Pass null to disable the override (e.g. in singleplayer).
        /// </summary>
        public void SetCollisionOverride(Func<int3, StateId?> collisionOverride)
        {
            _collisionOverride = collisionOverride;
        }

        /// <summary>
        ///     Sets the current position directly, used during teleport (spawn, benchmark).
        ///     Also sets previous position to avoid interpolation jump.
        /// </summary>
        public void Teleport(float3 position)
        {
            _currentPosition = position;
            PreviousPosition = position;
            _verticalSpeed = 0f;
        }

        /// <summary>
        ///     Sets velocity state for server reconciliation. The velocity in PlayerPhysicsState
        ///     stores vertical speed in the Y component; horizontal speeds are derived from input
        ///     each tick so they do not need explicit restoration.
        /// </summary>
        public void SetVelocity(float3 velocity)
        {
            _verticalSpeed = velocity.y;
        }

        /// <summary>
        ///     Restores physics mode flags from a server-authoritative PlayerPhysicsState.
        ///     Used during prediction reconciliation to snap to the server's mode state.
        ///     Bit 0 = OnGround, Bit 1 = IsFlying, Bit 2 = IsNoclip.
        /// </summary>
        public void SetFlags(byte flags)
        {
            OnGround = (flags & 1) != 0;
            IsFlying = (flags & 2) != 0;
            IsNoclip = (flags & 4) != 0;
        }

        /// <summary>
        ///     Sets position without resetting previous position (unlike Teleport).
        ///     Used during reconciliation replay where interpolation continuity is managed externally.
        /// </summary>
        public void SetPosition(float3 position)
        {
            _currentPosition = position;
        }

        /// <summary>
        ///     Called once per fixed tick by GameLoop with the current input snapshot.
        ///     All input reads come from the snapshot — no direct Keyboard/Mouse access.
        ///     Forward/right vectors are computed from <see cref="InputSnapshot.Yaw" />
        ///     using trig, removing the dependency on Transform.forward.
        /// </summary>
        public void TickWithSnapshot(float tickDt, in InputSnapshot snapshot)
        {
            if (ExternallyControlled)
            {
                return;
            }

            if (!SpawnReady)
            {
                return;
            }

            // Cache look direction for GetState() queries
            _yaw = snapshot.Yaw;
            _pitch = snapshot.Pitch;

            // Toggle fly mode (edge input from snapshot)
            if (snapshot.FlyTogglePressed)
            {
                IsFlying = !IsFlying;
                _verticalSpeed = 0f;

                if (IsFlying)
                {
                    OnGround = false;
                }
                else
                {
                    IsNoclip = false;
                }
            }

            // Toggle noclip (edge input from snapshot)
            if (snapshot.NoclipTogglePressed && IsFlying)
            {
                IsNoclip = !IsNoclip;
            }

            // Fly speed adjustment via scroll (accumulated in snapshot)
            if (IsFlying && snapshot.ScrollDelta != 0)
            {
                if (snapshot.ScrollDelta > 0)
                {
                    FlySpeed = math.clamp(
                        FlySpeed * FlySpeedScrollFactor, MinFlySpeed, MaxFlySpeed);
                }
                else
                {
                    FlySpeed = math.clamp(
                        FlySpeed / FlySpeedScrollFactor, MinFlySpeed, MaxFlySpeed);
                }
            }

            // Snapshot previous position for interpolation
            PreviousPosition = _currentPosition;

            // Detect water at feet and eye level
            UpdateWaterState();

            // Seed swim velocity when entering water so the player doesn't hit a wall
            if (IsInWater && !_wasInWater && !IsFlying)
            {
                float yawRad = math.radians(snapshot.Yaw);
                float forwardX = math.sin(yawRad);
                float forwardZ = math.cos(yawRad);

                float entrySpeed = IsSprinting ? _sprintSpeed : _walkSpeed;

                _horizontalSpeedX = forwardX * entrySpeed * 0.65f;
                _horizontalSpeedZ = forwardZ * entrySpeed * 0.65f;
            }

            _wasInWater = IsInWater;

            if (IsFlying)
            {
                TickFlyFromSnapshot(in snapshot, tickDt);
            }
            else if (IsInWater)
            {
                TickSwimFromSnapshot(in snapshot, tickDt);
            }
            else
            {
                TickWalkFromSnapshot(in snapshot, tickDt);
            }
        }

        private void TickWalkFromSnapshot(in InputSnapshot snapshot, float dt)
        {
            // Clear swim velocity when walking on land
            _horizontalSpeedX = 0f;
            _horizontalSpeedZ = 0f;

            float3 displacement = ComputeHorizontalFromSnapshot(
                in snapshot, dt, _walkSpeed, _sprintSpeed);

            _verticalSpeed += _gravity * dt;

            if (_verticalSpeed < _maxFallSpeed)
            {
                _verticalSpeed = _maxFallSpeed;
            }

            // Jump — edge input from snapshot
            if (snapshot.JumpPressed && OnGround)
            {
                _verticalSpeed = _jumpSpeed;
                OnGround = false;
            }

            displacement.y = _verticalSpeed * dt;

            SolidBlockQuery query = SolidBlockHelper.Build(
                _currentPosition, displacement, _playerHalfWidth, _playerHeight,
                _chunkDataReader, _nativeStateRegistry, _collisionOverride);

            CollisionResult result = VoxelCollider.Resolve(
                ref _currentPosition, ref displacement,
                _playerHalfWidth, _playerHeight, query);

            query.SolidMap.Dispose();

            OnGround = result.OnGround;

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
                in snapshot, dt, FlySpeed, FlySpeed);

            if (snapshot.JumpHeld)
            {
                displacement.y += FlySpeed * dt;
            }

            if (snapshot.Sprint)
            {
                displacement.y -= FlySpeed * dt;
            }

            if (IsNoclip)
            {
                _currentPosition += displacement;
            }
            else
            {
                SolidBlockQuery query = SolidBlockHelper.Build(
                    _currentPosition, displacement, _playerHalfWidth, _playerHeight,
                    _chunkDataReader, _nativeStateRegistry, _collisionOverride);

                CollisionResult result = VoxelCollider.Resolve(
                    ref _currentPosition, ref displacement,
                    _playerHalfWidth, _playerHeight, query);

                query.SolidMap.Dispose();
                OnGround = result.OnGround;
            }
        }

        /// <summary>
        ///     Computes horizontal movement from InputSnapshot fields and yaw.
        ///     Forward/right are derived from snapshot.Yaw using trig (no Transform read).
        /// </summary>
        private float3 ComputeHorizontalFromSnapshot(
            in InputSnapshot snapshot, float dt, float normalSpeed, float fastSpeed)
        {
            IsSprinting = snapshot.Sprint && !IsFlying;
            float speed = snapshot.Sprint ? fastSpeed : normalSpeed;

            // Derive forward/right from yaw (degrees) — no Transform.forward dependency
            float yawRad = math.radians(snapshot.Yaw);
            float3 forward = new(math.sin(yawRad), 0f, math.cos(yawRad));
            float3 right = new(math.cos(yawRad), 0f, -math.sin(yawRad));

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

        /// <summary>
        ///     Checks the block at feet position and eye position for fluid.
        ///     Uses LiquidData first, falls back to BlockStateCompact.IsFluid
        ///     for chunks where LiquidData hasn't been initialized yet.
        ///     Sets _inWater (feet in water) and _submerged (eyes in water).
        /// </summary>
        private void UpdateWaterState()
        {
            int3 feetBlock = new(
                (int)math.floor(_currentPosition.x),
                (int)math.floor(_currentPosition.y),
                (int)math.floor(_currentPosition.z));

            int3 eyeBlock = new(
                (int)math.floor(_currentPosition.x),
                (int)math.floor(_currentPosition.y + _playerEyeHeight),
                (int)math.floor(_currentPosition.z));

            IsInWater = IsBlockFluid(feetBlock);
            IsSubmerged = IsBlockFluid(eyeBlock);
        }

        private bool IsBlockFluid(int3 worldCoord)
        {
            byte liquidCell = _chunkDataReader.GetFluidLevel(worldCoord);

            if (liquidCell > 0)
            {
                return true;
            }

            // Fallback: check block state for fluid flag (covers chunks without LiquidData yet)
            StateId stateId = _chunkDataReader.GetBlock(worldCoord);

            if (stateId.Value == 0)
            {
                return false;
            }

            if (_nativeStateRegistry.States.IsCreated &&
                stateId.Value < _nativeStateRegistry.States.Length)
            {
                return _nativeStateRegistry.States[stateId.Value].IsFluid;
            }

            return false;
        }

        /// <summary>
        ///     Swim physics: reduced acceleration, higher drag, lower gravity, buoyancy via jump.
        ///     Minecraft-style parameters: accel 0.02, drag 0.8, gravity 0.02 down per tick.
        /// </summary>
        private void TickSwimFromSnapshot(in InputSnapshot snapshot, float dt)
        {
            float yawRad = math.radians(snapshot.Yaw);
            float3 forward = new(math.sin(yawRad), 0f, math.cos(yawRad));
            float3 right = new(math.cos(yawRad), 0f, -math.sin(yawRad));

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

            // Apply swim acceleration (sprint-swim uses higher accel for ~5 b/s terminal)
            float swimAccel = snapshot.Sprint ? _swimAcceleration * 2.6f : _swimAcceleration;
            _horizontalSpeedX += moveDir.x * swimAccel * dt;
            _horizontalSpeedZ += moveDir.z * swimAccel * dt;

            // Swim gravity (very low, floaty feel)
            _verticalSpeed += _swimGravity * dt;

            // Swim up when holding jump
            if (snapshot.JumpHeld)
            {
                _verticalSpeed += _swimUpSpeed * dt;
            }

            // Apply drag (per-tick multiplier, independent of dt for fixed tick rate)
            _horizontalSpeedX *= _swimDrag;
            _horizontalSpeedZ *= _swimDrag;
            _verticalSpeed *= _swimDrag;

            // Build displacement from velocity (per-second units, scaled by dt)
            float3 displacement = new(
                _horizontalSpeedX * dt,
                _verticalSpeed * dt,
                _horizontalSpeedZ * dt);

            SolidBlockQuery query = SolidBlockHelper.Build(
                _currentPosition, displacement, _playerHalfWidth, _playerHeight,
                _chunkDataReader, _nativeStateRegistry, _collisionOverride);

            CollisionResult result = VoxelCollider.Resolve(
                ref _currentPosition, ref displacement,
                _playerHalfWidth, _playerHeight, query);

            query.SolidMap.Dispose();

            OnGround = result.OnGround;

            if (result.OnGround && _verticalSpeed < 0f)
            {
                _verticalSpeed = 0f;
            }

            if (result.HitCeiling && _verticalSpeed > 0f)
            {
                _verticalSpeed = 0f;
            }

            IsSprinting = snapshot.Sprint;
        }

        /// <summary>
        ///     Returns a blittable snapshot of the current physics state.
        ///     Used for multiplayer state synchronization and client-side prediction.
        /// </summary>
        public PlayerPhysicsState GetState()
        {
            byte flags = 0;

            if (OnGround)
            {
                flags |= 1;
            }

            if (IsFlying)
            {
                flags |= 2;
            }

            if (IsNoclip)
            {
                flags |= 4;
            }

            return new PlayerPhysicsState
            {
                Position = _currentPosition,
                Velocity = new float3(0f, _verticalSpeed, 0f),
                Yaw = _yaw,
                Pitch = _pitch,
                Flags = flags,
            };
        }
    }
}
