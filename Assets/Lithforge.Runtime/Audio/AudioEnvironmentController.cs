using Lithforge.Runtime.Content.WorldGen;
using UnityEngine;

namespace Lithforge.Runtime.Audio
{
    /// <summary>
    /// Facade coordinating all environmental audio subsystems.
    /// <see cref="Tick"/> runs at 30 TPS (slow-changing state: biome, underwater, enclosure).
    /// <see cref="UpdateFrame"/> runs at frame rate (filter/reverb smoothing, crossfade).
    /// </summary>
    public sealed class AudioEnvironmentController
    {
        /// <summary>Underwater low-pass filter controller.</summary>
        private readonly UnderwaterAudioFilter _underwaterFilter;

        /// <summary>Enclosure detection probe for cave reverb.</summary>
        private readonly EnclosureProbe _enclosureProbe;

        /// <summary>Reverb controller driven by enclosure ratio.</summary>
        private readonly CaveReverbController _caveReverb;

        /// <summary>Runtime biome sampler for ambient audio selection.</summary>
        private readonly RuntimeBiomeSampler _biomeSampler;

        /// <summary>A/B crossfade player for biome ambient loops.</summary>
        private readonly BiomeAmbientPlayer _ambientPlayer;

        /// <summary>Random scatter sound player for environmental ambience.</summary>
        private readonly ScatterSoundPlayer _scatterPlayer;

        /// <summary>Player transform used for position-based biome sampling.</summary>
        private readonly Transform _playerTransform;

        /// <summary>Last biome index to detect biome transitions.</summary>
        private int _lastBiomeIndex = -1;

        /// <summary>Creates the environment controller with all audio subsystem references.</summary>
        public AudioEnvironmentController(
            UnderwaterAudioFilter underwaterFilter,
            EnclosureProbe enclosureProbe,
            CaveReverbController caveReverb,
            RuntimeBiomeSampler biomeSampler,
            BiomeAmbientPlayer ambientPlayer,
            ScatterSoundPlayer scatterPlayer,
            Transform playerTransform)
        {
            _underwaterFilter = underwaterFilter;
            _enclosureProbe = enclosureProbe;
            _caveReverb = caveReverb;
            _biomeSampler = biomeSampler;
            _ambientPlayer = ambientPlayer;
            _scatterPlayer = scatterPlayer;
            _playerTransform = playerTransform;
        }

        /// <summary>
        /// Called at 30 TPS from TickRegistry via AudioEnvironmentTickAdapter.
        /// </summary>
        public void Tick()
        {
            if (_underwaterFilter != null)
            {
                _underwaterFilter.Tick();
            }

            if (_enclosureProbe != null)
            {
                _enclosureProbe.Tick();
            }

            // Sample biome at player position
            if (_biomeSampler != null && _playerTransform != null)
            {
                _biomeSampler.Sample(
                    _playerTransform.position.x,
                    _playerTransform.position.z);

                int biomeIndex = _biomeSampler.CurrentBiomeIndex;

                if (biomeIndex != _lastBiomeIndex)
                {
                    _lastBiomeIndex = biomeIndex;
                    BiomeDefinition biome = _biomeSampler.CurrentBiome;

                    if (_ambientPlayer != null)
                    {
                        _ambientPlayer.SetBiomeClip(
                            biome != null ? biome.AmbientLoop : null);
                    }
                }
            }

            if (_scatterPlayer != null && _biomeSampler != null)
            {
                _scatterPlayer.Tick(_biomeSampler.CurrentBiome);
            }
        }

        /// <summary>
        /// Called each frame. Drives filter/reverb interpolation and ambient crossfade.
        /// </summary>
        public void UpdateFrame(float deltaTime)
        {
            if (_underwaterFilter != null)
            {
                _underwaterFilter.UpdateFrame(deltaTime);
            }

            if (_caveReverb != null)
            {
                _caveReverb.UpdateFrame(deltaTime);
            }

            if (_ambientPlayer != null)
            {
                _ambientPlayer.UpdateFrame(deltaTime);
            }
        }
    }
}
