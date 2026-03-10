using UnityEngine;

namespace Lithforge.Runtime.Content.Behaviors
{
    [CreateAssetMenu(fileName = "NewPlaySoundAction", menuName = "Lithforge/Behaviors/Play Sound")]
    public sealed class PlaySoundAction : BehaviorAction
    {
        [Tooltip("Sound clip to play")]
        [SerializeField] private AudioClip clip;

        [Tooltip("Volume")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1.0f;

        [Tooltip("Pitch")]
        [Range(0.1f, 3f)]
        [SerializeField] private float pitch = 1.0f;

        public AudioClip Clip
        {
            get { return clip; }
        }

        public float Volume
        {
            get { return volume; }
        }

        public float Pitch
        {
            get { return pitch; }
        }
    }
}
