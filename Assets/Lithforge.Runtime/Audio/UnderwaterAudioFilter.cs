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
        /// <summary>Camera transform for determining the player's head position.</summary>
        private readonly Transform _cameraTransform;

        /// <summary>Chunk manager for querying block state at the head position.</summary>
        private readonly ChunkManager _chunkManager;

        /// <summary>Unity low-pass filter component being driven.</summary>
        private readonly AudioLowPassFilter _filter;

        /// <summary>Interpolation speed for smoothing cutoff frequency changes.</summary>
        private readonly float _lerpSpeed;

        /// <summary>Native state registry for checking fluid flags on blocks.</summary>
        private readonly NativeStateRegistry _nativeStateRegistry;

        /// <summary>Low-pass cutoff frequency when on the surface (high = no filtering).</summary>
        private readonly float _surfaceCutoff;

        /// <summary>Low-pass cutoff frequency when submerged (low = muffled).</summary>
        private readonly float _underwaterCutoff;

        /// <summary>Target cutoff frequency that the filter interpolates toward.</summary>
        private float _targetCutoff;

        /// <summary>Creates the filter with chunk lookup, filter component, and frequency thresholds.</summary>
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

        /// <summary>True if the player's head is currently inside a fluid block.</summary>
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
