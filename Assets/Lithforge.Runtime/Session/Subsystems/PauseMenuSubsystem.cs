using System;
using System.Collections.Generic;

using Lithforge.Network;
using Lithforge.Network.Server;
using Lithforge.Network.Transport;
using Lithforge.Runtime.Network;
using Lithforge.Runtime.UI;
using Lithforge.Runtime.UI.Navigation;
using Lithforge.Runtime.World;

using UnityEngine;
using UnityEngine.UIElements;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class PauseMenuSubsystem : IGameSubsystem
    {
        private LanBroadcaster _lanBroadcaster;
        private INetworkTransport _lanTransport;

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

            bool isMultiplayer = context.Config is SessionConfig.Client
                or SessionConfig.Host
                or SessionConfig.DedicatedServer;

            GameState pauseState = isMultiplayer
                ? GameState.PausedOverlay
                : GameState.PausedFull;

            // Capture context for deferred resolution — SessionBridge is created
            // in SessionBridgeSubsystem.PostInitialize which runs after this.
            SessionContext capturedContext = context;

            // Build Open-to-LAN callback for singleplayer mode
            Action<Action<ushort>> onOpenToLan = null;

            if (context.Config is SessionConfig.Singleplayer sp)
            {
                onOpenToLan = onSuccess =>
                {
                    OpenToLan(capturedContext, sp, onSuccess);
                };
            }

            pauseMenuScreen.Initialize(
                panelSettings,
                settingsScreen,
                // onPause
                () =>
                {
                    if (capturedContext.TryGet(out GameLoopPoco loop))
                    {
                        loop.SetGameState(pauseState);
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
                            loop.SetGameState(pauseState);
                        }

                        pauseMenuScreen.HideOverlay();
                        bridge.QuitRequested = true;
                    }
                },
                onOpenToLan);

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

            if (_lanBroadcaster != null)
            {
                _lanBroadcaster.Dispose();
                _lanBroadcaster = null;
            }

            if (_lanTransport != null)
            {
                _lanTransport.Dispose();
                _lanTransport = null;
            }
        }

        /// <summary>
        ///     Opens the singleplayer session to LAN by adding a UTP transport to the
        ///     existing <see cref="CompositeTransport" /> and starting a
        ///     <see cref="LanBroadcaster" />.
        /// </summary>
        private void OpenToLan(
            SessionContext context,
            SessionConfig.Singleplayer sp,
            Action<ushort> onSuccess)
        {
            ILogger logger = context.App.Logger;

            if (!context.TryGet(out CompositeTransport composite))
            {
                logger.LogError("Open to LAN failed: no CompositeTransport in session context");
                return;
            }

            ushort port = NetworkConstants.DefaultPort;
            NetworkDriverWrapper utpTransport = new(logger);

            if (!utpTransport.Listen(port))
            {
                logger.LogError($"Open to LAN failed: could not listen on port {port}");
                utpTransport.Dispose();
                return;
            }

            composite.AddTransport(utpTransport);
            _lanTransport = utpTransport;

            // Start LAN broadcaster
            ContentHash contentHash = ContentHashComputer.Compute(context.Content.StateRegistry);

            LanBroadcaster broadcaster = new(new LanServerInfo
            {
                serverName = sp.DisplayName,
                gamePort = port,
                playerCount = 0,
                maxPlayers = 8,
                gameVersion = Application.version,
                contentHash = contentHash.ToString(),
                worldName = sp.DisplayName,
                gameMode = sp.GameMode.ToString(),
            });
            broadcaster.Start();
            _lanBroadcaster = broadcaster;

            // Wire player count updates
            if (context.TryGet(out ServerGameLoop serverGameLoop))
            {
                serverGameLoop.OnPlayerCountChanged = count =>
                    broadcaster.UpdatePlayerCount(count + 1);
            }

            context.Register(broadcaster);

            logger.LogInfo($"Opened to LAN on port {port}");
            onSuccess?.Invoke(port);
        }
    }
}
