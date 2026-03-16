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
        private readonly AudioReverbFilter _reverbFilter;
        private readonly EnclosureProbe _enclosureProbe;
        private readonly float _threshold;
        private readonly float _lerpSpeed;

        private float _targetDecayTime;
        private float _targetDryLevel;
        private float _targetRoom;

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
