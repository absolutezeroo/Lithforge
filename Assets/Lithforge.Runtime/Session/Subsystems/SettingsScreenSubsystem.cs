using System;
using System.Collections.Generic;

using Lithforge.Runtime.Audio;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.UI;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the in-game settings screen with live-apply sliders.</summary>
    public sealed class SettingsScreenSubsystem : IGameSubsystem
    {
        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "SettingsScreen";
            }
        }

        /// <summary>Depends on player, mesh store, and time-of-day for settings controls.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
            typeof(ChunkMeshStoreSubsystem),
            typeof(TimeOfDaySubsystem),
        };

        /// <summary>Only created for sessions that render.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>Creates the settings screen GameObject and registers it.</summary>
        public void Initialize(SessionContext context)
        {
            PanelSettings panelSettings = SessionInitArgsHolder.Current?.PanelSettings;

            GameObject settingsObject = new("SettingsScreen");
            SettingsScreen settingsScreen = settingsObject.AddComponent<SettingsScreen>();

            context.Register(settingsScreen);
        }

        /// <summary>Wires the settings screen to camera, time-of-day, mesh store, and audio mixer.</summary>
        public void PostInitialize(SessionContext context)
        {
            SettingsScreen settingsScreen = context.Get<SettingsScreen>();
            PanelSettings panelSettings = SessionInitArgsHolder.Current?.PanelSettings;

            PlayerTransformHolder player = context.Get<PlayerTransformHolder>();
            CameraController cameraController = context.Get<CameraController>();

            context.TryGet(out TimeOfDayController tod);
            ChunkMeshStore meshStore = context.Get<ChunkMeshStore>();

            // Need GameLoopPoco for render distance change notification
            // But SessionBridge isn't initialized yet. Use a captured lambda.
            GameLoopPoco gameLoop = null;
            Action<int> renderDistChanged = rd =>
            {
                gameLoop?.NotifyRenderDistanceChanged(rd);
            };

            KeyBindingConfig keyBindings = context.Get<KeyBindingConfig>();

            settingsScreen.Initialize(
                context.Get<ChunkManager>(),
                cameraController,
                tod,
                meshStore,
                panelSettings,
                context.App.UserPreferences,
                renderDistChanged,
                keyBindings);

            // Wire audio mixer
            AudioMixer mixer = Resources.Load<AudioMixer>("Audio/LithforgeMixer");

            if (mixer != null)
            {
                AudioMixerController audioMixerController = new(mixer);
                settingsScreen.SetAudioMixerController(audioMixerController);
            }

            settingsScreen.ApplyPersistedVolumes();
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>No owned disposable resources; settings screen is a GameObject.</summary>
        public void Dispose()
        {
        }
    }
}
