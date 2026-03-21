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
    /// <summary>
    ///     Subsystem that creates the full audio pipeline: SFX pool, block sounds,
    ///     footsteps, fall detection, underwater filter, cave reverb, and biome ambient.
    /// </summary>
    public sealed class AudioSubsystem : IGameSubsystem
    {
        /// <summary>The owned biome ambient audio player.</summary>
        private BiomeAmbientPlayer _biomeAmbientPlayer;

        /// <summary>The owned SFX audio source pool.</summary>
        private SfxSourcePool _sfxSourcePool;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "Audio";
            }
        }

        /// <summary>Depends on player, chunks, block interaction, and tick registry for audio wiring.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
            typeof(ChunkManagerSubsystem),
            typeof(BlockInteractionSubsystem),
            typeof(TickRegistrySubsystem),
        };

        /// <summary>Only created for sessions that render.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>Creates the complete audio pipeline and registers all components.</summary>
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
                context.App.Logger.LogWarning(
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
                    () => pcForAudio.PhysicsBody is
                    {
                        IsSprinting: true,
                    });
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

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>Disposes the SFX source pool and biome ambient player.</summary>
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
