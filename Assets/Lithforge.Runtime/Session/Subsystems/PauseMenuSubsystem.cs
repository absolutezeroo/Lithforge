using System;
using System.Collections.Generic;

using Lithforge.Runtime.UI;
using Lithforge.Runtime.UI.Navigation;
using Lithforge.Runtime.World;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class PauseMenuSubsystem : IGameSubsystem
    {
        public string Name
        {
            get
            {
                return "PauseMenu";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(SettingsScreenSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        public void Initialize(SessionContext context)
        {
            PanelSettings panelSettings = SessionInitArgsHolder.Current?.PanelSettings;

            GameObject pauseMenuObject = new("PauseMenuScreen");
            PauseMenuScreen pauseMenuScreen = pauseMenuObject.AddComponent<PauseMenuScreen>();

            context.Register(pauseMenuScreen);
        }

        public void PostInitialize(SessionContext context)
        {
            PauseMenuScreen pauseMenuScreen = context.Get<PauseMenuScreen>();
            SettingsScreen settingsScreen = context.Get<SettingsScreen>();
            PanelSettings panelSettings = SessionInitArgsHolder.Current?.PanelSettings;

            // Get SessionBridge for game state control
            // (SessionBridge may not exist yet, use a deferred approach)
            SessionBridge bridge = null;

            if (context.TryGet(out SessionBridge b))
            {
                bridge = b;
            }

            SessionBridge capturedBridge = bridge;
            GameLoopPoco capturedLoop = bridge?.GameLoop;

            pauseMenuScreen.Initialize(
                panelSettings,
                settingsScreen,
                // onPause
                () =>
                {
                    capturedLoop?.SetGameState(GameState.PausedFull);
                    pauseMenuScreen.Open();
                },
                // onResume
                () =>
                {
                    pauseMenuScreen.Close();
                    capturedLoop?.SetGameState(GameState.Playing);
                },
                // onOptions
                () =>
                {
                    pauseMenuScreen.HideOverlay();
                    settingsScreen.SetOnCloseCallback(() => { pauseMenuScreen.Open(); });
                    settingsScreen.Open();
                    settingsScreen.OpenedFromPause = true;
                },
                // onQuitToTitle
                () =>
                {
                    if (capturedBridge != null)
                    {
                        capturedLoop?.SetGameState(GameState.PausedFull);
                        pauseMenuScreen.HideOverlay();
                        capturedBridge.QuitRequested = true;
                    }
                });

            // Register with ScreenManager
            ScreenManager screenManager = context.App.ScreenManager;
            screenManager.Register(pauseMenuScreen);

            screenManager.OnEscapeEmpty = () =>
            {
                screenManager.Push(ScreenNames.Pause);
            };
        }

        public void Shutdown()
        {
        }

        public void Dispose()
        {
            // PauseMenuScreen GO destroyed in session cleanup
        }
    }
}
