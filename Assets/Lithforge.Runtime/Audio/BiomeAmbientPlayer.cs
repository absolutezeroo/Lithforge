using UnityEngine;
using UnityEngine.Audio;

namespace Lithforge.Runtime.Audio
{
    /// <summary>
    ///     Plays looping biome ambient audio using two AudioSources in an A/B
    ///     crossfade pattern. When the biome changes, the current source fades
    ///     out while the other fades in with the new clip.
    /// </summary>
    public sealed class BiomeAmbientPlayer
    {
        /// <summary>Duration of the A/B crossfade in seconds.</summary>
        private readonly float _crossfadeTime;

        /// <summary>First audio source in the A/B crossfade pair.</summary>
        private readonly AudioSource _sourceA;

        /// <summary>Second audio source in the A/B crossfade pair.</summary>
        private readonly AudioSource _sourceB;

        /// <summary>True if source A is the currently active (fading in) source.</summary>
        private bool _aIsActive;

        /// <summary>The currently assigned ambient clip for deduplication.</summary>
        private AudioClip _currentClip;

        /// <summary>Crossfade progress from 0 (start) to 1 (complete).</summary>
        private float _fadeProgress;

        /// <summary>True while a crossfade transition is in progress.</summary>
        private bool _isCrossfading;

        /// <summary>Creates the player with two child AudioSources for A/B crossfading.</summary>
        public BiomeAmbientPlayer(
            GameObject host,
            AudioMixerGroup ambientGroup,
            float crossfadeTime)
        {
            _crossfadeTime = crossfadeTime;

            GameObject childA = new("AmbientSourceA");
            childA.transform.SetParent(host.transform);
            _sourceA = childA.AddComponent<AudioSource>();
            ConfigureSource(_sourceA, ambientGroup);

            GameObject childB = new("AmbientSourceB");
            childB.transform.SetParent(host.transform);
            _sourceB = childB.AddComponent<AudioSource>();
            ConfigureSource(_sourceB, ambientGroup);

            _aIsActive = true;
        }

        /// <summary>
        ///     Sets the biome ambient clip. If different from current, starts a crossfade.
        ///     Pass null to fade out to silence.
        /// </summary>
        public void SetBiomeClip(AudioClip clip)
        {
            if (clip == _currentClip)
            {
                return;
            }

            _currentClip = clip;

            AudioSource incoming = _aIsActive ? _sourceB : _sourceA;
            incoming.clip = clip;
            incoming.volume = 0f;

            if (clip != null)
            {
                incoming.Play();
            }

            _aIsActive = !_aIsActive;
            _fadeProgress = 0f;
            _isCrossfading = true;
        }

        /// <summary>
        ///     Called each frame. Drives the crossfade volumes.
        /// </summary>
        public void UpdateFrame(float deltaTime)
        {
            if (!_isCrossfading)
            {
                return;
            }

            _fadeProgress += deltaTime / _crossfadeTime;

            if (_fadeProgress >= 1f)
            {
                _fadeProgress = 1f;
                _isCrossfading = false;
            }

            AudioSource active = _aIsActive ? _sourceA : _sourceB;
            AudioSource outgoing = _aIsActive ? _sourceB : _sourceA;

            active.volume = _fadeProgress;
            outgoing.volume = 1f - _fadeProgress;

            if (!_isCrossfading && outgoing.isPlaying)
            {
                outgoing.Stop();
                outgoing.clip = null;
            }
        }

        /// <summary>Stops both audio sources and destroys their GameObjects.</summary>
        public void Dispose()
        {
            if (_sourceA != null)
            {
                _sourceA.Stop();
                Object.Destroy(_sourceA.gameObject);
            }

            if (_sourceB != null)
            {
                _sourceB.Stop();
                Object.Destroy(_sourceB.gameObject);
            }
        }

        /// <summary>Configures an AudioSource for looping 2D ambient playback.</summary>
        private static void ConfigureSource(AudioSource source, AudioMixerGroup group)
        {
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;

            if (group != null)
            {
                source.outputAudioMixerGroup = group;
            }
        }
    }
}
