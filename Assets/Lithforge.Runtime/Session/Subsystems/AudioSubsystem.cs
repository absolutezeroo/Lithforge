using System;
using System.Collections.Generic;

using Lithforge.Runtime.Audio;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;

using UnityEngine;
using UnityEngine.Audio;

using AudioSettings = Lithforge.Runtime.Content.Settings.AudioSettings;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class AudioSubsystem : IGameSubsystem
    {
        private BiomeAmbientPlayer _biomeAmbientPlayer;
        private SfxSourcePool _sfxSourcePool;

        public string Name { get { return "Audio"; } }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
            typeof(ChunkManagerSubsystem),
            typeof(BlockInteractionSubsystem),
            typeof(TickRegistrySubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        public void Initialize(SessionContext context)
        {
            AudioSettings audioSettings = context.App.Settings.Audio;
            PlayerTransformHolder player = context.Get<PlayerTransformHolder>();
            Camera audioCamera = player.MainCamera;

            if (audioCamera == null || context.Content.SoundGroupRegistry == null)
            {
                return;
            }

            ChunkManager chunkManager = context.Get<ChunkManager>();

            // Audio mixer
            AudioMixer mixer = Resources.Load<AudioMixer>("Audio/LithforgeMixer");
            AudioMixerController audioMixerController = null;

            if (mixer != null)
            {
                audioMixerController = new AudioMixerController(mixer);
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[Lithforge] LithforgeMixer not found at Resources/Audio/. " +
                    "Audio will use AudioListener.volume fallback.");
            }

            // SFX source pool
            AudioMixerGroup sfxGroup =
                audioMixerController?.GetGroup("SFX");
            GameObject poolHost = new("SfxSourcePool");
            _sfxSourcePool = new SfxSourcePool(poolHost, audioSettings.SfxPoolSize, sfxGroup);

            // Block sound player
            BlockSoundPlayer blockSoundPlayer = new(
                context.Content.SoundGroupRegistry,
                context.Content.StateRegistry,
                _sfxSourcePool,
                audioSettings.SoundCooldownMs);

            // Wire to BlockInteraction
            if (context.TryGet(out BlockInteraction blockInteraction))
            {
                blockInteraction.SetBlockSoundPlayer(
                    blockSoundPlayer, audioSettings.MiningHitInterval);
            }

            // Footstep controller
            Transform playerTransformForAudio = audioCamera.transform.parent;
            PlayerController pcForAudio =
                playerTransformForAudio != null
                    ? playerTransformForAudio.GetComponent<PlayerController>()
                    : null;

            FootstepController footstepController = null;

            if (playerTransformForAudio != null && pcForAudio != null)
            {
                footstepController = new FootstepController(
                    blockSoundPlayer,
                    chunkManager,
                    context.Content.StateRegistry,
                    context.Content.NativeStateRegistry,
                    playerTransformForAudio,
                    audioSettings.FootstepDistance,
                    audioSettings.SprintFootstepDistance,
                    () => pcForAudio.OnGround,
                    () => pcForAudio.IsFlying,
                    () => pcForAudio.PhysicsBody != null && pcForAudio.PhysicsBody.IsSprinting);
            }

            // Fall sound detector
            FallSoundDetector fallSoundDetector = null;

            if (playerTransformForAudio != null && pcForAudio != null)
            {
                fallSoundDetector = new FallSoundDetector(
                    blockSoundPlayer,
                    chunkManager,
                    playerTransformForAudio,
                    audioSettings.FallSoundThreshold,
                    audioSettings.FallMaxVolume,
                    audioSettings.FallMaxHeight,
                    () => pcForAudio.OnGround,
                    () => pcForAudio.IsFlying);
            }

            // Environmental audio
            AudioLowPassFilter lowPassFilter =
                audioCamera.gameObject.AddComponent<AudioLowPassFilter>();
            lowPassFilter.cutoffFrequency = audioSettings.SurfaceCutoff;

            AudioReverbFilter reverbFilter =
                audioCamera.gameObject.AddComponent<AudioReverbFilter>();
            reverbFilter.reverbPreset = AudioReverbPreset.Off;

            UnderwaterAudioFilter underwaterFilter = new(
                chunkManager,
                context.Content.NativeStateRegistry,
                lowPassFilter,
                audioCamera.transform,
                audioSettings.UnderwaterCutoff,
                audioSettings.SurfaceCutoff,
                audioSettings.UnderwaterLerpSpeed);

            EnclosureProbe enclosureProbe = new(
                pos =>
                {
                    StateId sid = chunkManager.GetBlock(pos);
                    return sid.Value != 0 &&
                           context.Content.NativeStateRegistry.States.IsCreated &&
                           sid.Value < context.Content.NativeStateRegistry.States.Length &&
                           (context.Content.NativeStateRegistry.States[sid.Value].Flags &
                            BlockStateCompact.FlagFullCube) != 0;
                },
                audioCamera.transform,
                audioSettings.EnclosureRayCount,
                audioSettings.EnclosureMaxDistance,
                audioSettings.EnclosureUpdateTicks);

            CaveReverbController caveReverb = new(
                reverbFilter,
                enclosureProbe,
                audioSettings.EnclosureReverbThreshold,
                audioSettings.ReverbLerpSpeed);

            // Get seed for biome sampling
            long seed = 0;

            if (context.TryGet(out WorldMetadata metadata))
            {
                seed = metadata.Seed;
            }

            RuntimeBiomeSampler biomeSampler = new(
                context.Content.BiomeDefinitions, seed);

            AudioMixerGroup ambientGroup =
                audioMixerController?.GetGroup("Ambient");

            _biomeAmbientPlayer = new BiomeAmbientPlayer(
                audioCamera.gameObject,
                ambientGroup,
                audioSettings.AmbientCrossfadeTime);

            ScatterSoundPlayer scatterPlayer = new(
                _sfxSourcePool,
                playerTransformForAudio,
                audioSettings.ScatterMinInterval,
                audioSettings.ScatterMaxInterval,
                audioSettings.ScatterMinDistance,
                audioSettings.ScatterMaxDistance,
                () =>
                {
                    if (context.TryGet(out TimeOfDayController tod))
                    {
                        return tod.TimeOfDay;
                    }

                    return 0f;
                });

            AudioEnvironmentController audioEnvController = new(
                underwaterFilter,
                enclosureProbe,
                caveReverb,
                biomeSampler,
                _biomeAmbientPlayer,
                scatterPlayer,
                playerTransformForAudio);

            // Register in tick loop
            if (context.TryGet(out TickRegistry tickRegistry))
            {
                tickRegistry.Register(new AudioEnvironmentTickAdapter(audioEnvController));
            }

            context.Register(_sfxSourcePool);
            context.Register(footstepController);
            context.Register(fallSoundDetector);
            context.Register(audioEnvController);
        }

        public void PostInitialize(SessionContext context)
        {
        }

        public void Shutdown()
        {
        }

        public void Dispose()
        {
            if (_sfxSourcePool != null)
            {
                _sfxSourcePool.Dispose();
                _sfxSourcePool = null;
            }

            if (_biomeAmbientPlayer != null)
            {
                _biomeAmbientPlayer.Dispose();
                _biomeAmbientPlayer = null;
            }
        }
    }
}
