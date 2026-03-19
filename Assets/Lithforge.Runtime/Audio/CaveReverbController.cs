using UnityEngine;

namespace Lithforge.Runtime.Audio
{
    /// <summary>
    /// Drives an <see cref="AudioReverbFilter"/> based on the
    /// <see cref="EnclosureProbe"/>'s enclosure ratio. Higher enclosure
    /// means stronger reverb (cave/underground feel).
    /// </summary>
    public sealed class CaveReverbController
    {
        /// <summary>Unity AudioReverbFilter component being driven.</summary>
        private readonly AudioReverbFilter _reverbFilter;

        /// <summary>Enclosure probe providing the current enclosure ratio.</summary>
        private readonly EnclosureProbe _enclosureProbe;

        /// <summary>Minimum enclosure ratio required to activate reverb.</summary>
        private readonly float _threshold;

        /// <summary>Interpolation speed for smoothing reverb parameter changes.</summary>
        private readonly float _lerpSpeed;

        /// <summary>Target reverb decay time in seconds, driven by enclosure ratio.</summary>
        private float _targetDecayTime;

        /// <summary>Target dry level in dB.</summary>
        private float _targetDryLevel;

        /// <summary>Target room level in dB, driven by enclosure ratio.</summary>
        private float _targetRoom;

        /// <summary>Creates the controller and initializes the reverb filter to silent defaults.</summary>
        public CaveReverbController(
            AudioReverbFilter reverbFilter,
            EnclosureProbe enclosureProbe,
            float threshold,
            float lerpSpeed)
        {
            _reverbFilter = reverbFilter;
            _enclosureProbe = enclosureProbe;
            _threshold = threshold;
            _lerpSpeed = lerpSpeed;

            if (_reverbFilter != null)
            {
                _reverbFilter.reverbPreset = AudioReverbPreset.Off;
                _reverbFilter.dryLevel = 0f;
                _reverbFilter.room = -10000f;
                _reverbFilter.decayTime = 0.1f;
            }
        }

        /// <summary>
        /// Called each frame. Smoothly drives reverb parameters from enclosure ratio.
        /// </summary>
        public void UpdateFrame(float deltaTime)
        {
            if (_reverbFilter == null || _enclosureProbe == null)
            {
                return;
            }

            float enclosure = _enclosureProbe.EnclosureRatio;

            if (enclosure > _threshold)
            {
                // Map enclosure [threshold..1] to reverb strength [0..1]
                float strength = (enclosure - _threshold) / (1f - _threshold);
                strength = Mathf.Clamp01(strength);

                _targetDecayTime = Mathf.Lerp(0.5f, 4.0f, strength);
                _targetRoom = Mathf.Lerp(-10000f, -600f, strength);
                _targetDryLevel = 0f;
            }
            else
            {
                // Open air — minimal reverb
                _targetDecayTime = 0.1f;
                _targetRoom = -10000f;
                _targetDryLevel = 0f;
            }

            float t = _lerpSpeed * deltaTime;
            _reverbFilter.decayTime = Mathf.Lerp(_reverbFilter.decayTime, _targetDecayTime, t);
            _reverbFilter.room = Mathf.Lerp(_reverbFilter.room, _targetRoom, t);
            _reverbFilter.dryLevel = Mathf.Lerp(_reverbFilter.dryLevel, _targetDryLevel, t);
        }
    }
}
