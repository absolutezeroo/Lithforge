using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Audio
{
    /// <summary>
    /// Data-driven definition of sounds for a block material group (e.g. stone, wood, grass).
    /// Each event type has an array of clips; one is chosen at random on playback.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSoundGroup", menuName = "Lithforge/Audio/Sound Group Definition", order = 0)]
    public sealed class SoundGroupDefinition : ScriptableObject
    {
        [FormerlySerializedAs("_groupName"), Header("Identity")]
        [Tooltip("Sound group name (must match BlockDefinition.soundGroup)")]
        [SerializeField] private string groupName = "";

        [FormerlySerializedAs("_breakClips"), Header("Break")]
        [Tooltip("Clips played when a block of this group is broken")]
        [SerializeField] private AudioClip[] breakClips;

        [FormerlySerializedAs("_breakVolume")]
        [Range(0f, 2f)]
        [SerializeField] private float breakVolume = 1.0f;

        [FormerlySerializedAs("_breakPitchMin")]
        [Range(0.5f, 2f)]
        [SerializeField] private float breakPitchMin = 0.8f;

        [FormerlySerializedAs("_breakPitchMax")]
        [Range(0.5f, 2f)]
        [SerializeField] private float breakPitchMax = 1.0f;

        [FormerlySerializedAs("_placeClips"), Header("Place")]
        [Tooltip("Clips played when a block of this group is placed")]
        [SerializeField] private AudioClip[] placeClips;

        [FormerlySerializedAs("_placeVolume")]
        [Range(0f, 2f)]
        [SerializeField] private float placeVolume = 1.0f;

        [FormerlySerializedAs("_placePitchMin")]
        [Range(0.5f, 2f)]
        [SerializeField] private float placePitchMin = 0.8f;

        [FormerlySerializedAs("_placePitchMax")]
        [Range(0.5f, 2f)]
        [SerializeField] private float placePitchMax = 1.0f;

        [FormerlySerializedAs("_stepClips"), Header("Step")]
        [Tooltip("Clips played for footsteps on this material")]
        [SerializeField] private AudioClip[] stepClips;

        [FormerlySerializedAs("_stepVolume")]
        [Range(0f, 2f)]
        [SerializeField] private float stepVolume = 0.5f;

        [FormerlySerializedAs("_stepPitchMin")]
        [Range(0.5f, 2f)]
        [SerializeField] private float stepPitchMin = 0.9f;

        [FormerlySerializedAs("_stepPitchMax")]
        [Range(0.5f, 2f)]
        [SerializeField] private float stepPitchMax = 1.1f;

        [FormerlySerializedAs("_hitClips"), Header("Hit")]
        [Tooltip("Clips played during mining progress")]
        [SerializeField] private AudioClip[] hitClips;

        [FormerlySerializedAs("_hitVolume")]
        [Range(0f, 2f)]
        [SerializeField] private float hitVolume = 0.6f;

        [FormerlySerializedAs("_hitPitchMin")]
        [Range(0.5f, 2f)]
        [SerializeField] private float hitPitchMin = 0.5f;

        [FormerlySerializedAs("_hitPitchMax")]
        [Range(0.5f, 2f)]
        [SerializeField] private float hitPitchMax = 0.7f;

        [FormerlySerializedAs("_fallClips"), Header("Fall")]
        [Tooltip("Clips played when landing on this material after a fall")]
        [SerializeField] private AudioClip[] fallClips;

        [FormerlySerializedAs("_fallVolume")]
        [Range(0f, 2f)]
        [SerializeField] private float fallVolume = 1.0f;

        [FormerlySerializedAs("_fallPitchMin")]
        [Range(0.5f, 2f)]
        [SerializeField] private float fallPitchMin = 0.75f;

        [FormerlySerializedAs("_fallPitchMax")]
        [Range(0.5f, 2f)]
        [SerializeField] private float fallPitchMax = 1.0f;

        /// <summary>Sound group name that must match BlockDefinition.soundGroup for lookup.</summary>
        public string GroupName
        {
            get { return groupName; }
        }

        /// <summary>
        /// Returns a random clip for the given event type, or null if no clips are assigned.
        /// </summary>
        public AudioClip GetRandomClip(SoundEventType eventType, System.Random rng)
        {
            AudioClip[] clips = GetClips(eventType);

            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            return clips[rng.Next(clips.Length)];
        }

        /// <summary>
        /// Returns the base volume for the given event type.
        /// </summary>
        public float GetVolume(SoundEventType eventType)
        {
            return eventType switch
            {
                SoundEventType.Break => breakVolume,
                SoundEventType.Place => placeVolume,
                SoundEventType.Step => stepVolume,
                SoundEventType.Hit => hitVolume,
                SoundEventType.Fall => fallVolume,
                _ => 1.0f,
            };
        }

        /// <summary>
        /// Returns a random pitch within the min/max range for the given event type.
        /// </summary>
        public float GetRandomPitch(SoundEventType eventType, System.Random rng)
        {
            float min;
            float max;

            switch (eventType)
            {
                case SoundEventType.Break:
                    min = breakPitchMin;
                    max = breakPitchMax;
                    break;
                case SoundEventType.Place:
                    min = placePitchMin;
                    max = placePitchMax;
                    break;
                case SoundEventType.Step:
                    min = stepPitchMin;
                    max = stepPitchMax;
                    break;
                case SoundEventType.Hit:
                    min = hitPitchMin;
                    max = hitPitchMax;
                    break;
                case SoundEventType.Fall:
                    min = fallPitchMin;
                    max = fallPitchMax;
                    break;
                default:
                    min = 0.9f;
                    max = 1.1f;
                    break;
            }

            return min + (float)rng.NextDouble() * (max - min);
        }

        /// <summary>Returns the clip array for the given event type, or null if unassigned.</summary>
        private AudioClip[] GetClips(SoundEventType eventType)
        {
            return eventType switch
            {
                SoundEventType.Break => breakClips,
                SoundEventType.Place => placeClips,
                SoundEventType.Step => stepClips,
                SoundEventType.Hit => hitClips,
                SoundEventType.Fall => fallClips,
                _ => null,
            };
        }

        /// <summary>Editor callback that auto-fills the group name from the asset name if empty.</summary>
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(groupName))
            {
                groupName = name;
            }
        }
    }
}
