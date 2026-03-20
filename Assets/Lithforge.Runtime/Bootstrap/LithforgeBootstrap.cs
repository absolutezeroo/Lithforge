using System;
using System.Collections;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Session;
using Lithforge.Runtime.UI.Navigation;
using Lithforge.Runtime.UI.Screens;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;

using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

using AppContext = Lithforge.Runtime.Session.AppContext;
using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Bootstrap
{
    /// <summary>
    ///     Top-level MonoBehaviour that orchestrates the entire game lifecycle.
    ///     Creates app-lifetime resources in Awake, then loops between the main menu
    ///     and game sessions driven by <see cref="SessionOrchestrator" />.
    /// </summary>
    public sealed class LithforgeBootstrap : MonoBehaviour
    {
        /// <summary>Material for opaque voxel rendering assigned in the Inspector.</summary>
        [FormerlySerializedAs("_voxelMaterial"), SerializeField]
        private Material voxelMaterial;

        /// <summary>Compute shader for GPU frustum culling assigned in the Inspector.</summary>
        [FormerlySerializedAs("_frustumCullShader"), SerializeField]
        private ComputeShader frustumCullShader;

        /// <summary>Compute shader for Hi-Z pyramid generation assigned in the Inspector.</summary>
        [FormerlySerializedAs("_hiZGenerateShader"), SerializeField]
        private ComputeShader hiZGenerateShader;

        /// <summary>Compute shader for GPU buffer copy operations assigned in the Inspector.</summary>
        [SerializeField]
        private ComputeShader bufferCopyShader;

        /// <summary>App-lifetime context holding settings, logger, and shared resources.</summary>
        private AppContext _appContext;

        /// <summary>Session config set by menu screens to trigger session start.</summary>
        private SessionConfig _pendingSession;

        /// <summary>Persisted list of saved multiplayer server entries.</summary>
        private SavedServerList _savedServerList;

        /// <summary>Screen stack manager for modal navigation.</summary>
        private ScreenManager _screenManager;

        /// <summary>Creates app-lifetime resources: settings, logger, profiler, screen manager.</summary>
        private void Awake()
        {
            ILogger logger = new UnityLogger();
            LoadedSettings settings = SettingsLoader.Load(logger);
            UserPreferences userPreferences = UserPreferences.Load(logger);

            IFrameProfiler frameProfiler = new FrameProfiler();
            frameProfiler.Enabled = settings.Debug.EnableProfiling;
            IPipelineStats pipelineStats = new PipelineStats();
            pipelineStats.Enabled = settings.Debug.EnableProfiling;

            _screenManager = gameObject.AddComponent<ScreenManager>();
            _screenManager.SetLogger(logger);
            _savedServerList = new SavedServerList(logger);

            _appContext = new AppContext(
                settings, logger, frameProfiler, pipelineStats,
                userPreferences, _screenManager, _savedServerList, this)
            {
                VoxelMaterial = voxelMaterial, FrustumCullShader = frustumCullShader, HiZGenerateShader = hiZGenerateShader, BufferCopyShader = bufferCopyShader,
            };

        }

        /// <summary>Main lifecycle loop: alternates between main menu and game sessions.</summary>
        private IEnumerator Start()
        {
            PanelSettings panelSettings =
                Resources.Load<PanelSettings>("DefaultPanelSettings");

            CreateMenuScreens(panelSettings);

            while (true)
            {
                _pendingSession = null;
                _screenManager.ClearAll();
                _screenManager.Push(ScreenNames.MainMenu);

                while (_pendingSession == null)
                {
                    yield return null;
                }

                _screenManager.ClearAll();

                yield return StartCoroutine(
                    SessionOrchestrator.Run(_pendingSession, _appContext, () => { }));
            }
        }

        /// <summary>Synchronous save fallback when the application is quit mid-session.</summary>
        private void OnDestroy()
        {
            // Synchronous save fallback when the application is quit mid-session
            GameSession session = _appContext?.CurrentSession;

            if (session == null)
            {
                return;
            }

            try
            {
                session.Shutdown();

                if (session.Context.TryGet(out AutoSaveManager autoSave))
                {
                    autoSave.SaveMetadataOnly();
                }

                if (session.Context.TryGet(out AsyncChunkSaver asyncSaver))
                {
                    asyncSaver.Flush();
                }

                if (session.Context.TryGet(out WorldStorage ws)
                    && session.Context.TryGet(out ChunkManager cm))
                {
                    cm.SaveAllChunks(ws);
                    ws.FlushAll();
                }
            }
            catch (Exception ex)
            {
                _appContext?.Logger?.LogError($"[Lithforge] Error during shutdown save: {ex}");
            }

            session.Dispose();
            SessionOrchestrator.DisposeContentResources(session.Context.Content, _appContext?.Logger);
            _appContext.CurrentSession = null;
        }

        /// <summary>Creates all main menu screens (main menu, world selection, host settings, join game).</summary>
        private void CreateMenuScreens(PanelSettings panelSettings)
        {
            Action<SessionConfig> onSessionSelected = session =>
            {
                _pendingSession = session;
            };

            GameObject mainMenuObj = new("MainMenuScreen");
            MainMenuScreen mainMenu = mainMenuObj.AddComponent<MainMenuScreen>();
            mainMenu.Initialize(panelSettings, _screenManager);
            _screenManager.Register(mainMenu);

            GameObject worldSelObj = new("WorldSelectionScreen");
            WorldSelectionScreen worldSelection = worldSelObj.AddComponent<WorldSelectionScreen>();
            worldSelection.Initialize(panelSettings, onSessionSelected, _screenManager);
            _screenManager.Register(worldSelection);

            GameObject hostSettingsObj = new("HostSettingsModal");
            HostSettingsModal hostSettings = hostSettingsObj.AddComponent<HostSettingsModal>();
            hostSettings.Initialize(panelSettings, _screenManager, onSessionSelected, _appContext.Logger);
            _screenManager.Register(hostSettings);

            GameObject joinGameObj = new("JoinGameScreen");
            JoinGameScreen joinGame = joinGameObj.AddComponent<JoinGameScreen>();
            joinGame.Initialize(panelSettings, _screenManager, _savedServerList, clientConfig =>
            {
                _pendingSession = clientConfig;
            }, _appContext.Logger);
            _screenManager.Register(joinGame);

            GameObject connectionObj = new("ConnectionProgressScreen");
            ConnectionProgressScreen connectionProgress =
                connectionObj.AddComponent<ConnectionProgressScreen>();
            connectionProgress.Initialize(panelSettings, _screenManager, () =>
            {
                _pendingSession = null;
                _screenManager.PopTo(ScreenNames.JoinGame);
            });
            _screenManager.Register(connectionProgress);
        }
    }
}
