using System;

using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using Unity.Mathematics;

using UnityEngine;

namespace Lithforge.Runtime.Audio
{
    /// <summary>
    ///     Detects landing after a fall and plays the appropriate fall sound
    ///     from the block the player lands on. Volume scales with fall height.
    /// </summary>
    public sealed class FallSoundDetector
    {
        /// <summary>Block sound player for playing fall impact sounds.</summary>
        private readonly BlockSoundPlayer _blockSoundPlayer;

        /// <summary>Chunk manager for querying the block at the player's feet.</summary>
        private readonly ChunkManager _chunkManager;

        /// <summary>Maximum fall height used for volume scaling.</summary>
        private readonly float _fallMaxHeight;

        /// <summary>Maximum volume for fall impact sounds.</summary>
        private readonly float _fallMaxVolume;

        /// <summary>Minimum fall distance in blocks before a fall sound plays.</summary>
        private readonly float _fallThreshold;

        /// <summary>Delegate returning true if the player is currently flying.</summary>
        private readonly Func<bool> _isFlying;

        /// <summary>Delegate returning true if the player is currently on the ground.</summary>
        private readonly Func<bool> _isOnGround;

        /// <summary>Player transform for reading vertical position.</summary>
        private readonly Transform _playerTransform;

        /// <summary>Highest Y reached during the current fall for height calculation.</summary>
        private float _highestY;

        /// <summary>True while actively tracking a fall (player is airborne).</summary>
        private bool _tracking;

        /// <summary>Previous frame's on-ground state for edge detection.</summary>
        private bool _wasOnGround;

        /// <summary>Creates the detector with references to audio, physics, and player state.</summary>
        public FallSoundDetector(
            BlockSoundPlayer blockSoundPlayer,
            ChunkManager chunkManager,
            Transform playerTransform,
            float fallThreshold,
            float fallMaxVolume,
            float fallMaxHeight,
            Func<bool> isOnGround,
            Func<bool> isFlying)
        {
            _blockSoundPlayer = blockSoundPlayer;
            _chunkManager = chunkManager;
            _playerTransform = playerTransform;
            _fallThreshold = fallThreshold;
            _fallMaxVolume = fallMaxVolume;
            _fallMaxHeight = fallMaxHeight;
            _isOnGround = isOnGround;
            _isFlying = isFlying;
            _wasOnGround = true;
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

            bool onGround = _isOnGround();
            bool flying = _isFlying();
            float currentY = _playerTransform.position.y;

            if (flying)
            {
                _wasOnGround = onGround;
                _tracking = false;

                return;
            }

            if (_wasOnGround && !onGround)
            {
                // Just left the ground — start tracking fall
                _highestY = currentY;
                _tracking = true;
            }
            else if (_tracking && !onGround)
            {
                // Still falling — track highest point
                if (currentY > _highestY)
                {
                    _highestY = currentY;
                }
            }
            else if (!_wasOnGround && onGround && _tracking)
            {
                // Just landed
                float fallHeight = _highestY - currentY;
                _tracking = false;

                if (fallHeight >= _fallThreshold)
                {
                    // Query block at feet
                    int3 feetBlock = new(
                        (int)math.floor(_playerTransform.position.x),
                        (int)math.floor(_playerTransform.position.y) - 1,
                        (int)math.floor(_playerTransform.position.z));

                    StateId stateId = _chunkManager.GetBlock(feetBlock);

                    if (stateId.Value != 0)
                    {
                        _blockSoundPlayer.PlayBlockSound(
                            stateId, SoundEventType.Fall, feetBlock);
                    }
                }
            }

            _wasOnGround = onGround;
        }
    }
}
