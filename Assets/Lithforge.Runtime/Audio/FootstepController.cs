using System;

using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using Unity.Mathematics;

using UnityEngine;

namespace Lithforge.Runtime.Audio
{
    /// <summary>
    ///     Plays footstep sounds based on horizontal distance traveled.
    ///     Queries the block under the player's feet to determine sound group.
    ///     Supports material layering: if the surface block is non-full-cube
    ///     (e.g. a carpet), the underlying block's step sound plays at reduced volume.
    /// </summary>
    public sealed class FootstepController
    {
        /// <summary>Block sound player for playing step sounds.</summary>
        private readonly BlockSoundPlayer _blockSoundPlayer;

        /// <summary>Chunk manager for querying block state at the player's feet.</summary>
        private readonly ChunkManager _chunkManager;

        /// <summary>Delegate returning true if the player is currently flying.</summary>
        private readonly Func<bool> _isFlying;

        /// <summary>Delegate returning true if the player is currently on the ground.</summary>
        private readonly Func<bool> _isOnGround;

        /// <summary>Delegate returning true if the player is currently sprinting.</summary>
        private readonly Func<bool> _isSprinting;

        /// <summary>Native state registry for checking block flags (full-cube test for layering).</summary>
        private readonly NativeStateRegistry _nativeStateRegistry;

        /// <summary>Player transform for tracking horizontal movement distance.</summary>
        private readonly Transform _playerTransform;

        /// <summary>Horizontal distance threshold between footsteps while sprinting.</summary>
        private readonly float _sprintThreshold;

        /// <summary>Managed state registry for looking up block entries by StateId.</summary>
        private readonly StateRegistry _stateRegistry;

        /// <summary>Horizontal distance threshold between footsteps while walking.</summary>
        private readonly float _walkThreshold;

        /// <summary>Accumulated horizontal distance since the last footstep sound.</summary>
        private float _accumulatedDistance;

        /// <summary>True after the first Update call has captured the initial position.</summary>
        private bool _initialized;

        /// <summary>Previous frame X position for distance calculation.</summary>
        private float _previousX;

        /// <summary>Previous frame Z position for distance calculation.</summary>
        private float _previousZ;

        /// <summary>Creates the controller with block sound, movement thresholds, and player state delegates.</summary>
        public FootstepController(
            BlockSoundPlayer blockSoundPlayer,
            ChunkManager chunkManager,
            StateRegistry stateRegistry,
            NativeStateRegistry nativeStateRegistry,
            Transform playerTransform,
            float walkThreshold,
            float sprintThreshold,
            Func<bool> isOnGround,
            Func<bool> isFlying,
            Func<bool> isSprinting)
        {
            _blockSoundPlayer = blockSoundPlayer;
            _chunkManager = chunkManager;
            _stateRegistry = stateRegistry;
            _nativeStateRegistry = nativeStateRegistry;
            _playerTransform = playerTransform;
            _walkThreshold = walkThreshold;
            _sprintThreshold = sprintThreshold;
            _isOnGround = isOnGround;
            _isFlying = isFlying;
            _isSprinting = isSprinting;
        }

        /// <summary>
        ///     Call each frame from LateUpdate after player position is interpolated.
        /// </summary>
        public void Update()
        {
            if (_playerTransform == null)
            {
                return;
            }

            float currentX = _playerTransform.position.x;
            float currentZ = _playerTransform.position.z;

            if (!_initialized)
            {
                _previousX = currentX;
                _previousZ = currentZ;
                _initialized = true;

                return;
            }

            float dx = currentX - _previousX;
            float dz = currentZ - _previousZ;
            float distMoved = math.sqrt(dx * dx + dz * dz);
            _previousX = currentX;
            _previousZ = currentZ;

            if (distMoved < 0.001f)
            {
                return;
            }

            // Only play footsteps when on ground and not flying
            if (!_isOnGround() || _isFlying())
            {
                _accumulatedDistance = 0f;

                return;
            }

            _accumulatedDistance += distMoved;

            bool sprinting = _isSprinting();
            float threshold = sprinting ? _sprintThreshold : _walkThreshold;

            if (_accumulatedDistance >= threshold)
            {
                _accumulatedDistance -= threshold;

                // Query block directly below feet
                int3 feetBlock = new(
                    (int)math.floor(_playerTransform.position.x),
                    (int)math.floor(_playerTransform.position.y) - 1,
                    (int)math.floor(_playerTransform.position.z));

                StateId stateId = _chunkManager.GetBlock(feetBlock);

                if (stateId.Value != 0)
                {
                    _blockSoundPlayer.PlayBlockSound(stateId, SoundEventType.Step, feetBlock);

                    // Material layering: if surface block is non-full-cube, play underlying block
                    if (_nativeStateRegistry.States.IsCreated &&
                        stateId.Value < _nativeStateRegistry.States.Length)
                    {
                        BlockStateCompact compact = _nativeStateRegistry.States[stateId.Value];

                        if ((compact.Flags & BlockStateCompact.FlagFullCube) == 0)
                        {
                            int3 belowBlock = new(feetBlock.x, feetBlock.y - 1, feetBlock.z);
                            StateId belowState = _chunkManager.GetBlock(belowBlock);

                            if (belowState.Value != 0)
                            {
                                StateRegistryEntry belowEntry = _stateRegistry.GetEntryForState(belowState);

                                if (belowEntry != null)
                                {
                                    Vector3 pos = new(
                                        feetBlock.x + 0.5f, feetBlock.y + 0.5f, feetBlock.z + 0.5f);
                                    _blockSoundPlayer.PlayGroupSound(
                                        belowEntry.SoundGroup, SoundEventType.Step, pos);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
