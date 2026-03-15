using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Behaviors
{
    /// <summary>
    /// Behavior action that plays an audio clip at the block's position when triggered.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPlaySoundAction", menuName = "Lithforge/Behaviors/Play Sound")]
    public sealed class PlaySoundAction : BehaviorAction
    {
        /// <summary>Audio clip to play at the block's world position.</summary>
        [FormerlySerializedAs("_clip"),Tooltip("Sound clip to play")]
        [SerializeField] private AudioClip clip;

        /// <summary>Playback volume (0 = silent, 1 = full).</summary>
        [FormerlySerializedAs("_volume"),Tooltip("Volume")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1.0f;

        /// <summary>Pitch multiplier — values below 1 deepen the sound, above 1 raise it.</summary>
        [FormerlySerializedAs("_pitch"),Tooltip("Pitch")]
        [Range(0.1f, 3f)]
        [SerializeField] private float pitch = 1.0f;

        /// <summary>Audio clip to play at the block's world position.</summary>
        public AudioClip Clip
        {
            get { return clip; }
        }

        /// <summary>Playback volume (0 = silent, 1 = full).</summary>
        public float Volume
        {
            get { return volume; }
        }

        /// <summary>Pitch multiplier — values below 1 deepen the sound, above 1 raise it.</summary>
        public float Pitch
        {
            get { return pitch; }
        }
    }
}
