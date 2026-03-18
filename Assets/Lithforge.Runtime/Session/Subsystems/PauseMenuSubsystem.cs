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

            // Capture context for deferred resolution — SessionBridge is created
            // in SessionBridgeSubsystem.PostInitialize which runs after this.
            SessionContext capturedContext = context;

            pauseMenuScreen.Initialize(
                panelSettings,
                settingsScreen,
                // onPause
                () =>
                {
                    if (capturedContext.TryGet(out GameLoopPoco loop))
                    {
                        loop.SetGameState(GameState.PausedFull);
                    }

                    pauseMenuScreen.Open();
                },
                // onResume
                () =>
                {
                    pauseMenuScreen.Close();

                    if (capturedContext.TryGet(out GameLoopPoco loop))
                    {
                        loop.SetGameState(GameState.Playing);
                    }
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
                    if (capturedContext.TryGet(out SessionBridge bridge))
                    {
                        if (capturedContext.TryGet(out GameLoopPoco loop))
                        {
                            loop.SetGameState(GameState.PausedFull);
                        }

                        pauseMenuScreen.HideOverlay();
                        bridge.QuitRequested = true;
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
