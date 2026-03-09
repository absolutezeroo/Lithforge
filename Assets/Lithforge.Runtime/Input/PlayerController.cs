using System;
using Lithforge.Physics;
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
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = PhysicsConstants.WalkSpeed;
        [SerializeField] private float sprintSpeed = PhysicsConstants.SprintSpeed;

        private ChunkManager _chunkManager;
        private NativeStateRegistry _nativeStateRegistry;
        private GameLoop _gameLoop;
        private float _verticalSpeed;
        private bool _onGround;
        private Func<int3, bool> _isSolidDelegate;

        /// <summary>
        /// True if the player is standing on solid ground.
        /// </summary>
        public bool OnGround
        {
            get { return _onGround; }
        }

        public void Initialize(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            GameLoop gameLoop)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
            _gameLoop = gameLoop;

            // Cache the delegate to avoid allocation each frame
            _isSolidDelegate = (int3 coord) => SolidBlockQuery.IsSolid(
                coord, _chunkManager, _nativeStateRegistry);
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

            HandleCursorToggle(keyboard);

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            float dt = Time.deltaTime;

            // Compute horizontal displacement from input
            float3 displacement = ComputeHorizontalDisplacement(keyboard, dt);

            // Apply gravity to vertical speed (blocks/sec, persistent across frames)
            _verticalSpeed += PhysicsConstants.Gravity * dt;

            if (_verticalSpeed < PhysicsConstants.MaxFallSpeed)
            {
                _verticalSpeed = PhysicsConstants.MaxFallSpeed;
            }

            // Jump
            if (keyboard.spaceKey.wasPressedThisFrame && _onGround)
            {
                _verticalSpeed = PhysicsConstants.JumpSpeed;
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
                PhysicsConstants.PlayerHalfWidth,
                PhysicsConstants.PlayerHeight,
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

        private float3 ComputeHorizontalDisplacement(Keyboard keyboard, float dt)
        {
            float speed = keyboard.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;

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


        private void HandleCursorToggle(Keyboard keyboard)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }
    }
}
