using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Settings
{
    /// <summary>
    ///     Tuning parameters for the audio system. Loaded from Resources/Settings/AudioSettings.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioSettings", menuName = "Lithforge/Settings/Audio Settings", order = 6)]
    public sealed class AudioSettings : ScriptableObject
    {
        /// <summary>Number of pre-allocated AudioSources in the one-shot SFX pool.</summary>
        [FormerlySerializedAs("_sfxPoolSize"), Header("SFX Pool"), Tooltip("Number of pre-allocated AudioSources for one-shot SFX"), Range(8, 64), SerializeField]
        private int sfxPoolSize = 24;

        /// <summary>Minimum milliseconds between sounds of the same group+event combination.</summary>
        [FormerlySerializedAs("_soundCooldownMs"), Header("Cooldown"), Tooltip("Minimum time between sounds of the same group+event (ms)"), Range(10, 200), SerializeField]
        private int soundCooldownMs = 50;

        /// <summary>Horizontal distance in blocks walked before a footstep sound plays.</summary>
        [FormerlySerializedAs("_footstepDistance"), Header("Footsteps"), Tooltip("Distance walked before a footstep sound plays"), Range(0.5f, 5f), SerializeField]
        private float footstepDistance = 2.5f;

        [FormerlySerializedAs("_sprintFootstepDistance"), Tooltip("Distance walked before a footstep sound plays while sprinting"), Range(0.3f, 3f), SerializeField]
        private float sprintFootstepDistance = 1.5f;

        [FormerlySerializedAs("_fallDamageThreshold"), Header("Fall"), Tooltip("Minimum fall height in blocks before a fall sound plays"), Range(1f, 10f), SerializeField]
        private float fallSoundThreshold = 3f;

        [FormerlySerializedAs("_fallMaxVolume"), Tooltip("Maximum fall sound volume (reached at max fall height)"), Range(0.5f, 2f), SerializeField]
        private float fallMaxVolume = 1.0f;

        [FormerlySerializedAs("_fallMaxHeight"), Tooltip("Fall height at which volume reaches maximum"), Range(5f, 50f), SerializeField]
        private float fallMaxHeight = 20f;

        [FormerlySerializedAs("_miningHitInterval"), Header("Mining"), Tooltip("Number of ticks between mining hit sounds"), Range(1, 20), SerializeField]
        private int miningHitInterval = 4;

        [FormerlySerializedAs("_underwaterCutoff"), Header("Underwater"), Tooltip("Low-pass cutoff frequency when fully submerged (Hz)"), Range(100f, 1000f), SerializeField]
        private float underwaterCutoff = 300f;

        [FormerlySerializedAs("_surfaceCutoff"), Tooltip("Low-pass cutoff frequency on the surface (Hz)"), SerializeField]
        private float surfaceCutoff = 22000f;

        [FormerlySerializedAs("_underwaterLerpSpeed"), Tooltip("Speed of low-pass transition (higher = faster)"), Range(1f, 20f), SerializeField]
        private float underwaterLerpSpeed = 8f;

        [FormerlySerializedAs("_enclosureRayCount"), Header("Cave Reverb"), Tooltip("Number of DDA rays for enclosure probing"), Range(4, 24), SerializeField]
        private int enclosureRayCount = 10;

        [FormerlySerializedAs("_enclosureMaxDistance"), Tooltip("Maximum ray distance for enclosure probing (blocks)"), Range(5, 40), SerializeField]
        private int enclosureMaxDistance = 20;

        [FormerlySerializedAs("_enclosureUpdateTicks"), Tooltip("Ticks between enclosure re-evaluations"), Range(5, 60), SerializeField]
        private int enclosureUpdateTicks = 15;

        [FormerlySerializedAs("_enclosureReverbThreshold"), Tooltip("Enclosure ratio above which reverb starts increasing"), Range(0.1f, 0.9f), SerializeField]
        private float enclosureReverbThreshold = 0.5f;

        [FormerlySerializedAs("_reverbLerpSpeed"), Tooltip("Speed of reverb level transitions"), Range(0.5f, 10f), SerializeField]
        private float reverbLerpSpeed = 3f;

        [FormerlySerializedAs("_ambientCrossfadeTime"), Header("Ambient"), Tooltip("Crossfade duration when switching biome ambient loops (seconds)"), Range(1f, 15f), SerializeField]
        private float ambientCrossfadeTime = 6f;

        [FormerlySerializedAs("_scatterMinInterval"), Header("Scatter"), Tooltip("Minimum interval between scatter sounds (seconds)"), Range(5f, 60f), SerializeField]
        private float scatterMinInterval = 10f;

        [FormerlySerializedAs("_scatterMaxInterval"), Tooltip("Maximum interval between scatter sounds (seconds)"), Range(10f, 120f), SerializeField]
        private float scatterMaxInterval = 30f;

        [FormerlySerializedAs("_scatterMinDistance"), Tooltip("Minimum distance for scatter sound placement from player"), Range(2f, 20f), SerializeField]
        private float scatterMinDistance = 5f;

        [FormerlySerializedAs("_scatterMaxDistance"), Tooltip("Maximum distance for scatter sound placement from player"), Range(5f, 40f), SerializeField]
        private float scatterMaxDistance = 15f;

        /// <summary>Number of pre-allocated AudioSources in the one-shot SFX pool.</summary>
        public int SfxPoolSize { get { return sfxPoolSize; } }

        /// <summary>Minimum milliseconds between sounds of the same group+event combination.</summary>
        public int SoundCooldownMs { get { return soundCooldownMs; } }

        /// <summary>Horizontal distance in blocks walked before a footstep sound plays.</summary>
        public float FootstepDistance { get { return footstepDistance; } }

        /// <summary>Horizontal distance in blocks walked before a footstep sound plays while sprinting.</summary>
        public float SprintFootstepDistance { get { return sprintFootstepDistance; } }

        /// <summary>Minimum fall height in blocks before a fall sound plays.</summary>
        public float FallSoundThreshold { get { return fallSoundThreshold; } }

        /// <summary>Maximum fall sound volume, reached at FallMaxHeight.</summary>
        public float FallMaxVolume { get { return fallMaxVolume; } }

        /// <summary>Fall height in blocks at which volume reaches maximum.</summary>
        public float FallMaxHeight { get { return fallMaxHeight; } }

        /// <summary>Number of ticks between mining hit sounds.</summary>
        public int MiningHitInterval { get { return miningHitInterval; } }

        /// <summary>Low-pass cutoff frequency in Hz when fully submerged.</summary>
        public float UnderwaterCutoff { get { return underwaterCutoff; } }

        /// <summary>Low-pass cutoff frequency in Hz on the surface (effectively no filtering).</summary>
        public float SurfaceCutoff { get { return surfaceCutoff; } }

        /// <summary>Speed of low-pass filter transition (higher = faster).</summary>
        public float UnderwaterLerpSpeed { get { return underwaterLerpSpeed; } }

        /// <summary>Number of DDA rays for enclosure probing.</summary>
        public int EnclosureRayCount { get { return enclosureRayCount; } }

        /// <summary>Maximum ray distance in blocks for enclosure probing.</summary>
        public int EnclosureMaxDistance { get { return enclosureMaxDistance; } }

        /// <summary>Ticks between enclosure re-evaluations.</summary>
        public int EnclosureUpdateTicks { get { return enclosureUpdateTicks; } }

        /// <summary>Enclosure ratio above which reverb starts increasing.</summary>
        public float EnclosureReverbThreshold { get { return enclosureReverbThreshold; } }

        /// <summary>Speed of reverb level transitions (higher = faster).</summary>
        public float ReverbLerpSpeed { get { return reverbLerpSpeed; } }

        /// <summary>Crossfade duration in seconds when switching biome ambient loops.</summary>
        public float AmbientCrossfadeTime { get { return ambientCrossfadeTime; } }

        /// <summary>Minimum interval in seconds between scatter sounds.</summary>
        public float ScatterMinInterval { get { return scatterMinInterval; } }

        /// <summary>Maximum interval in seconds between scatter sounds.</summary>
        public float ScatterMaxInterval { get { return scatterMaxInterval; } }

        /// <summary>Minimum distance from the player for scatter sound placement.</summary>
        public float ScatterMinDistance { get { return scatterMinDistance; } }

        /// <summary>Maximum distance from the player for scatter sound placement.</summary>
        public float ScatterMaxDistance { get { return scatterMaxDistance; } }
    }
}
