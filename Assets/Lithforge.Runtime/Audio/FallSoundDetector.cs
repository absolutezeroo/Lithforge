using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime.Audio
{
    /// <summary>
    /// Detects landing after a fall and plays the appropriate fall sound
    /// from the block the player lands on. Volume scales with fall height.
    /// </summary>
    public sealed class FallSoundDetector
    {
        private readonly BlockSoundPlayer _blockSoundPlayer;
        private readonly ChunkManager _chunkManager;
        private readonly Transform _playerTransform;
        private readonly float _fallThreshold;
        private readonly float _fallMaxVolume;
        private readonly float _fallMaxHeight;

        private readonly System.Func<bool> _isOnGround;
        private readonly System.Func<bool> _isFlying;

        private bool _wasOnGround;
        private float _highestY;
        private bool _tracking;

        public FallSoundDetector(
            BlockSoundPlayer blockSoundPlayer,
            ChunkManager chunkManager,
            Transform playerTransform,
            float fallThreshold,
            float fallMaxVolume,
            float fallMaxHeight,
            System.Func<bool> isOnGround,
            System.Func<bool> isFlying)
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
        /// Call each frame from LateUpdate after player position is interpolated.
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
