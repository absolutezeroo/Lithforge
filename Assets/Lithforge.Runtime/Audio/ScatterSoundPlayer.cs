using Lithforge.Runtime.Content.WorldGen;
using UnityEngine;
using UnityEngine.Audio;

namespace Lithforge.Runtime.Audio
{
    /// <summary>
    /// Plays random ambient scatter sounds (bird calls, crickets, etc.) at
    /// random offsets around the player. Time-of-day filtering restricts
    /// certain sounds to day or night.
    /// </summary>
    public sealed class ScatterSoundPlayer
    {
        private readonly SfxSourcePool _pool;
        private readonly Transform _playerTransform;
        private readonly float _minInterval;
        private readonly float _maxInterval;
        private readonly float _minDistance;
        private readonly float _maxDistance;
        private readonly System.Func<float> _getTimeOfDay;
        private readonly System.Random _rng;

        private float _nextScatterTime;

        /// <summary>
        /// Creates a scatter sound player.
        /// </summary>
        /// <param name="pool">SFX source pool to play clips.</param>
        /// <param name="playerTransform">Player transform for position offset.</param>
        /// <param name="minInterval">Minimum seconds between scatters.</param>
        /// <param name="maxInterval">Maximum seconds between scatters.</param>
        /// <param name="minDistance">Minimum distance from player.</param>
        /// <param name="maxDistance">Maximum distance from player.</param>
        /// <param name="getTimeOfDay">Returns normalized time of day [0..1] where 0.25=noon.</param>
        public ScatterSoundPlayer(
            SfxSourcePool pool,
            Transform playerTransform,
            float minInterval,
            float maxInterval,
            float minDistance,
            float maxDistance,
            System.Func<float> getTimeOfDay)
        {
            _pool = pool;
            _playerTransform = playerTransform;
            _minInterval = minInterval;
            _maxInterval = maxInterval;
            _minDistance = minDistance;
            _maxDistance = maxDistance;
            _getTimeOfDay = getTimeOfDay;
            _rng = new System.Random();

            _nextScatterTime = Time.time + RandomInterval();
        }

        /// <summary>
        /// Attempts to play a scatter sound if the timer has elapsed.
        /// Called at tick rate.
        /// </summary>
        public void Tick(BiomeDefinition currentBiome)
        {
            if (_playerTransform == null || currentBiome == null)
            {
                return;
            }

            if (Time.time < _nextScatterTime)
            {
                return;
            }

            _nextScatterTime = Time.time + RandomInterval();

            AudioClip[] scatterClips = currentBiome.ScatterClips;

            if (scatterClips == null || scatterClips.Length == 0)
            {
                return;
            }

            // Time-of-day filtering
            int restriction = currentBiome.ScatterTimeRestriction;

            if (restriction != 0)
            {
                float timeOfDay = _getTimeOfDay != null ? _getTimeOfDay() : 0f;
                bool isDay = timeOfDay >= 0.0f && timeOfDay < 0.5f;

                if (restriction == 1 && !isDay)
                {
                    return;
                }

                if (restriction == 2 && isDay)
                {
                    return;
                }
            }

            // Pick random clip
            AudioClip clip = scatterClips[_rng.Next(scatterClips.Length)];

            if (clip == null)
            {
                return;
            }

            // Compute random position around player
            float distance = _minDistance + (float)_rng.NextDouble() * (_maxDistance - _minDistance);
            float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;

            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * distance,
                (float)_rng.NextDouble() * 4f - 2f,
                Mathf.Sin(angle) * distance);

            Vector3 position = _playerTransform.position + offset;

            _pool.Play(clip, position, 0.6f, 0.9f + (float)_rng.NextDouble() * 0.2f);
        }

        private float RandomInterval()
        {
            return _minInterval + (float)_rng.NextDouble() * (_maxInterval - _minInterval);
        }
    }
}
