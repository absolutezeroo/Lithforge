using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using Unity.Mathematics;

using UnityEngine;

namespace Lithforge.Runtime.Audio
{
    /// <summary>
    ///     Applies a low-pass filter when the player's head is submerged in a fluid block.
    ///     Ticked at 30 TPS to check submersion; filter cutoff interpolated at frame rate.
    /// </summary>
    public sealed class UnderwaterAudioFilter
    {
        private readonly Transform _cameraTransform;
        private readonly ChunkManager _chunkManager;
        private readonly AudioLowPassFilter _filter;
        private readonly float _lerpSpeed;
        private readonly NativeStateRegistry _nativeStateRegistry;
        private readonly float _surfaceCutoff;
        private readonly float _underwaterCutoff;

        private float _targetCutoff;

        public UnderwaterAudioFilter(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            AudioLowPassFilter filter,
            Transform cameraTransform,
            float underwaterCutoff,
            float surfaceCutoff,
            float lerpSpeed)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
            _filter = filter;
            _cameraTransform = cameraTransform;
            _underwaterCutoff = underwaterCutoff;
            _surfaceCutoff = surfaceCutoff;
            _lerpSpeed = lerpSpeed;
            _targetCutoff = surfaceCutoff;

            if (_filter != null)
            {
                _filter.cutoffFrequency = surfaceCutoff;
                _filter.lowpassResonanceQ = 1.0f;
            }
        }

        public bool IsUnderwater { get; private set; }

        /// <summary>
        ///     Called at 30 TPS. Checks if the camera position is inside a fluid block.
        /// </summary>
        public void Tick()
        {
            if (_cameraTransform == null)
            {
                return;
            }

            Vector3 headPos = _cameraTransform.position;
            int3 headBlock = new(
                (int)math.floor(headPos.x),
                (int)math.floor(headPos.y),
                (int)math.floor(headPos.z));

            StateId stateId = _chunkManager.GetBlock(headBlock);
            bool underwater = false;

            if (_nativeStateRegistry.States.IsCreated &&
                stateId.Value < _nativeStateRegistry.States.Length)
            {
                BlockStateCompact compact = _nativeStateRegistry.States[stateId.Value];
                underwater = (compact.Flags & BlockStateCompact.FlagFluid) != 0;
            }

            IsUnderwater = underwater;
            _targetCutoff = underwater ? _underwaterCutoff : _surfaceCutoff;
        }

        /// <summary>
        ///     Called each frame. Smoothly interpolates the low-pass filter cutoff.
        /// </summary>
        public void UpdateFrame(float deltaTime)
        {
            if (_filter == null)
            {
                return;
            }

            _filter.cutoffFrequency = Mathf.Lerp(
                _filter.cutoffFrequency, _targetCutoff, _lerpSpeed * deltaTime);
            _filter.lowpassResonanceQ = IsUnderwater ? 1.5f : 1.0f;
        }
    }
}
