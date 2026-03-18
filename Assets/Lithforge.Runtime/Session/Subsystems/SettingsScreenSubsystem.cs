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
    public sealed class SettingsScreenSubsystem : IGameSubsystem
    {
        public string Name
        {
            get
            {
                return "SettingsScreen";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
            typeof(ChunkMeshStoreSubsystem),
            typeof(TimeOfDaySubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        public void Initialize(SessionContext context)
        {
            PanelSettings panelSettings = SessionInitArgsHolder.Current?.PanelSettings;

            GameObject settingsObject = new("SettingsScreen");
            SettingsScreen settingsScreen = settingsObject.AddComponent<SettingsScreen>();

            context.Register(settingsScreen);
        }

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

            settingsScreen.Initialize(
                context.Get<ChunkManager>(),
                cameraController,
                tod,
                meshStore,
                panelSettings,
                context.App.UserPreferences,
                renderDistChanged);

            // Wire audio mixer
            AudioMixer mixer = Resources.Load<AudioMixer>("Audio/LithforgeMixer");

            if (mixer != null)
            {
                AudioMixerController audioMixerController = new(mixer);
                settingsScreen.SetAudioMixerController(audioMixerController);
            }

            settingsScreen.ApplyPersistedVolumes();
        }

        public void Shutdown()
        {
        }

        public void Dispose()
        {
        }
    }
}
