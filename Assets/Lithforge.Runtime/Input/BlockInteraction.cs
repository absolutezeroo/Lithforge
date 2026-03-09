using System;
using System.Collections.Generic;
using Lithforge.Physics;
using Lithforge.Runtime.Rendering;
using VoxelRaycastHit = Lithforge.Physics.RaycastHit;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lithforge.Runtime.Input
{
    /// <summary>
    /// Handles block breaking (left click, progressive mining) and
    /// block placement (right click) via voxel raycasting.
    /// Attached to the camera GameObject.
    /// </summary>
    public sealed class BlockInteraction : MonoBehaviour
    {
        private ChunkManager _chunkManager;
        private NativeStateRegistry _nativeStateRegistry;
        private StateRegistry _stateRegistry;
        private BlockHighlight _blockHighlight;
        private Func<int3, bool> _isSolidDelegate;

        // Mining state
        private bool _isMining;
        private int3 _miningBlockCoord;
        private float _miningProgress;
        private float _miningRequiredTime;

        // Placement cooldown
        private float _placeCooldown;
        private const float _placeCooldownTime = 0.25f;

        // Reusable list for dirty chunks
        private readonly List<int3> _dirtiedChunks = new List<int3>();

        // Block to place (for now, hardcode stone — inventory integration comes in Milestone 4)
        private StateId _placeBlockState;

        public void Initialize(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            StateRegistry stateRegistry,
            BlockHighlight blockHighlight,
            StateId defaultPlaceBlock)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
            _stateRegistry = stateRegistry;
            _blockHighlight = blockHighlight;
            _placeBlockState = defaultPlaceBlock;
            _isSolidDelegate = IsSolid;
        }

        private void Update()
        {
            if (_chunkManager == null || Cursor.lockState != CursorLockMode.Locked)
            {
                _blockHighlight.Hide();

                return;
            }

            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                return;
            }

            // Update placement cooldown
            if (_placeCooldown > 0f)
            {
                _placeCooldown -= Time.deltaTime;
            }

            // Cast ray from camera
            VoxelRaycastHit hit = VoxelRaycast.Cast(
                new float3(transform.position.x, transform.position.y, transform.position.z),
                new float3(transform.forward.x, transform.forward.y, transform.forward.z),
                PhysicsConstants.InteractionRange,
                _isSolidDelegate);

            if (hit.DidHit)
            {
                _blockHighlight.SetTarget(hit.BlockCoord);
                HandleMining(mouse, hit);
                HandlePlacement(mouse, hit);
            }
            else
            {
                _blockHighlight.Hide();
                ResetMining();
            }
        }

        private void HandleMining(Mouse mouse, VoxelRaycastHit hit)
        {
            if (!mouse.leftButton.isPressed)
            {
                ResetMining();

                return;
            }

            // Check if target changed
            if (!_isMining || !_miningBlockCoord.Equals(hit.BlockCoord))
            {
                StartMining(hit.BlockCoord);
            }

            // Accumulate mining progress
            _miningProgress += Time.deltaTime;

            if (_miningProgress >= _miningRequiredTime)
            {
                BreakBlock(hit.BlockCoord);
                ResetMining();
            }
        }

        private void StartMining(int3 blockCoord)
        {
            _isMining = true;
            _miningBlockCoord = blockCoord;
            _miningProgress = 0f;

            // Look up hardness from BlockDefinition
            StateId stateId = _chunkManager.GetBlock(blockCoord);
            BlockDefinition definition = _stateRegistry.GetDefinitionForState(stateId);

            if (definition != null)
            {
                // Break time = hardness * multiplier (no tool = 5x, correct tool = 1.5x)
                // For now, without tool system, use the base hardness as seconds
                _miningRequiredTime = (float)(definition.Hardness * 1.5);

                // Minimum break time of 50ms (instant break for hardness 0)
                if (_miningRequiredTime < 0.05f)
                {
                    _miningRequiredTime = 0.05f;
                }
            }
            else
            {
                _miningRequiredTime = 0.05f;
            }
        }

        private void BreakBlock(int3 blockCoord)
        {
            _dirtiedChunks.Clear();
            _chunkManager.SetBlock(blockCoord, StateId.Air, _dirtiedChunks);
        }

        private void HandlePlacement(Mouse mouse, VoxelRaycastHit hit)
        {
            if (!mouse.rightButton.wasPressedThisFrame || _placeCooldown > 0f)
            {
                return;
            }

            // Place on the adjacent face
            int3 placeCoord = hit.BlockCoord + hit.Normal;

            // Check the target position is air
            StateId existing = _chunkManager.GetBlock(placeCoord);

            if (existing != StateId.Air)
            {
                return;
            }

            _dirtiedChunks.Clear();
            _chunkManager.SetBlock(placeCoord, _placeBlockState, _dirtiedChunks);
            _placeCooldown = _placeCooldownTime;
        }

        private void ResetMining()
        {
            _isMining = false;
            _miningProgress = 0f;
            _miningRequiredTime = 0f;
        }

        private bool IsSolid(int3 worldCoord)
        {
            StateId stateId = _chunkManager.GetBlock(worldCoord);
            BlockStateCompact compact = _nativeStateRegistry.States[stateId.Value];

            return compact.CollisionShape != 0;
        }

        /// <summary>
        /// Gets the current mining progress as a value from 0 to 1.
        /// </summary>
        public float MiningProgress
        {
            get
            {
                if (!_isMining || _miningRequiredTime <= 0f)
                {
                    return 0f;
                }

                return Mathf.Clamp01(_miningProgress / _miningRequiredTime);
            }
        }

        /// <summary>
        /// True if the player is currently mining a block.
        /// </summary>
        public bool IsMining
        {
            get { return _isMining; }
        }

        /// <summary>
        /// Sets the block state to use when placing blocks.
        /// Will be driven by the hotbar inventory in Milestone 4.
        /// </summary>
        public void SetPlaceBlock(StateId stateId)
        {
            _placeBlockState = stateId;
        }
    }
}
