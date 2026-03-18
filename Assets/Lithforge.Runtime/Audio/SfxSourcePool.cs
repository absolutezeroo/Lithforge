using UnityEngine;
using UnityEngine.Audio;

namespace Lithforge.Runtime.Audio
{
    /// <summary>
    /// Pre-allocated pool of AudioSource components for one-shot spatial SFX.
    /// Uses round-robin acquisition. Each frame, finished sources are marked available.
    /// Zero per-frame allocation.
    /// </summary>
    public sealed class SfxSourcePool
    {
        private readonly AudioSource[] _sources;
        private readonly bool[] _inUse;
        private int _nextIndex;

        /// <summary>
        /// Creates the pool with a fixed number of AudioSources parented to a host GameObject.
        /// </summary>
        /// <param name="host">Parent GameObject for all pooled sources.</param>
        /// <param name="poolSize">Number of AudioSources to pre-allocate.</param>
        /// <param name="mixerGroup">AudioMixerGroup to assign to all sources (can be null).</param>
        public SfxSourcePool(GameObject host, int poolSize, AudioMixerGroup mixerGroup)
        {
            _sources = new AudioSource[poolSize];
            _inUse = new bool[poolSize];

            for (int i = 0; i < poolSize; i++)
            {
                GameObject child = new("SfxSource_" + i);
                child.transform.SetParent(host.transform);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1.0f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 1f;
                source.maxDistance = 32f;
                source.dopplerLevel = 0f;

                if (mixerGroup != null)
                {
                    source.outputAudioMixerGroup = mixerGroup;
                }

                _sources[i] = source;
                _inUse[i] = false;
            }
        }

        /// <summary>
        /// Acquires an AudioSource, configures it for playback, and plays the clip.
        /// Returns null if no source is available (all playing).
        /// </summary>
        public AudioSource Play(AudioClip clip, Vector3 position, float volume, float pitch)
        {
            if (clip == null)
            {
                return null;
            }

            int startIndex = _nextIndex;

            for (int attempt = 0; attempt < _sources.Length; attempt++)
            {
                int idx = (_nextIndex + attempt) % _sources.Length;

                if (!_inUse[idx] || !_sources[idx].isPlaying)
                {
                    AudioSource source = _sources[idx];
                    source.transform.position = position;
                    source.clip = clip;
                    source.volume = volume;
                    source.pitch = pitch;
                    source.Play();
                    _inUse[idx] = true;
                    _nextIndex = (idx + 1) % _sources.Length;

                    return source;
                }
            }

            // All sources busy — steal the oldest
            AudioSource stolen = _sources[_nextIndex];
            stolen.Stop();
            stolen.transform.position = position;
            stolen.clip = clip;
            stolen.volume = volume;
            stolen.pitch = pitch;
            stolen.Play();
            _nextIndex = (_nextIndex + 1) % _sources.Length;

            return stolen;
        }

        /// <summary>
        /// Marks finished sources as available. Call once per frame from LateUpdate.
        /// </summary>
        public void ReleaseFinished()
        {
            for (int i = 0; i < _sources.Length; i++)
            {
                if (_inUse[i] && !_sources[i].isPlaying)
                {
                    _inUse[i] = false;
                    _sources[i].clip = null;
                }
            }
        }

        /// <summary>
        /// Stops all playing sources and destroys their GameObjects.
        /// </summary>
        public void Dispose()
        {
            for (int i = 0; i < _sources.Length; i++)
            {
                if (_sources[i] != null)
                {
                    _sources[i].Stop();
                    Object.Destroy(_sources[i].gameObject);
                }
            }
        }
    }
}
