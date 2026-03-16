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
        private readonly UnderwaterAudioFilter _underwaterFilter;
        private readonly EnclosureProbe _enclosureProbe;
        private readonly CaveReverbController _caveReverb;
        private readonly RuntimeBiomeSampler _biomeSampler;
        private readonly BiomeAmbientPlayer _ambientPlayer;
        private readonly ScatterSoundPlayer _scatterPlayer;
        private readonly Transform _playerTransform;

        private int _lastBiomeIndex = -1;

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
