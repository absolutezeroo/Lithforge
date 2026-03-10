using UnityEngine;

namespace Lithforge.Runtime.Content.Behaviors
{
    [CreateAssetMenu(fileName = "NewPlaySoundAction", menuName = "Lithforge/Behaviors/Play Sound")]
    public sealed class PlaySoundActionSO : BehaviorActionSO
    {
        [Tooltip("Sound clip to play")]
        [SerializeField] private AudioClip _clip;

        [Tooltip("Volume")]
        [Range(0f, 1f)]
        [SerializeField] private float _volume = 1.0f;

        [Tooltip("Pitch")]
        [Range(0.1f, 3f)]
        [SerializeField] private float _pitch = 1.0f;

        public AudioClip Clip
        {
            get { return _clip; }
        }

        public float Volume
        {
            get { return _volume; }
        }

        public float Pitch
        {
            get { return _pitch; }
        }
    }
}
