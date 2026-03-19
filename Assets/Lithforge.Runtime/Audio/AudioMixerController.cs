using UnityEngine;
using UnityEngine.Audio;

namespace Lithforge.Runtime.Audio
{
    /// <summary>
    ///     Wraps a Unity AudioMixer, providing linear-to-dB volume conversion
    ///     and named group lookups. The mixer must expose parameters named
    ///     "MasterVolume", "SFXVolume", "MusicVolume", "AmbientVolume".
    /// </summary>
    public sealed class AudioMixerController
    {
        /// <summary>Exposed parameter name for master volume on the AudioMixer.</summary>
        private const string MasterVolumeParam = "MasterVolume";

        /// <summary>Exposed parameter name for SFX volume on the AudioMixer.</summary>
        private const string SfxVolumeParam = "SFXVolume";

        /// <summary>Exposed parameter name for music volume on the AudioMixer.</summary>
        private const string MusicVolumeParam = "MusicVolume";

        /// <summary>Exposed parameter name for ambient volume on the AudioMixer.</summary>
        private const string AmbientVolumeParam = "AmbientVolume";

        /// <summary>Creates the controller wrapping the given AudioMixer.</summary>
        public AudioMixerController(AudioMixer mixer)
        {
            Mixer = mixer;
        }

        /// <summary>
        ///     The underlying AudioMixer, for direct access if needed.
        /// </summary>
        public AudioMixer Mixer { get; }

        /// <summary>Sets the master volume from a linear [0..1] value.</summary>
        public void SetMasterVolume(float linear)
        {
            SetVolume(MasterVolumeParam, linear);
        }

        /// <summary>Sets the SFX volume from a linear [0..1] value.</summary>
        public void SetSfxVolume(float linear)
        {
            SetVolume(SfxVolumeParam, linear);
        }

        /// <summary>Sets the music volume from a linear [0..1] value.</summary>
        public void SetMusicVolume(float linear)
        {
            SetVolume(MusicVolumeParam, linear);
        }

        /// <summary>Sets the ambient volume from a linear [0..1] value.</summary>
        public void SetAmbientVolume(float linear)
        {
            SetVolume(AmbientVolumeParam, linear);
        }

        /// <summary>
        ///     Returns the AudioMixerGroup with the given name, or null if not found.
        /// </summary>
        public AudioMixerGroup GetGroup(string name)
        {
            if (Mixer == null)
            {
                return null;
            }

            AudioMixerGroup[] groups = Mixer.FindMatchingGroups(name);

            if (groups is
                {
                    Length: > 0,
                })
            {
                return groups[0];
            }

            return null;
        }

        /// <summary>Converts a linear [0..1] volume to dB and sets it on the mixer parameter.</summary>
        private void SetVolume(string paramName, float linear)
        {
            if (Mixer == null)
            {
                return;
            }

            // Convert linear [0..1] to dB [-80..0]
            float db = linear > 0.0001f
                ? 20f * Mathf.Log10(linear)
                : -80f;

            db = Mathf.Clamp(db, -80f, 0f);
            Mixer.SetFloat(paramName, db);
        }
    }
}
