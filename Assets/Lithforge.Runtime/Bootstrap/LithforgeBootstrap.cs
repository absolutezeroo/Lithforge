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
        [FormerlySerializedAs("_voxelMaterial"), SerializeField]
        private Material voxelMaterial;

        [FormerlySerializedAs("_frustumCullShader"), SerializeField]
        private ComputeShader frustumCullShader;

        [FormerlySerializedAs("_hiZGenerateShader"), SerializeField]
        private ComputeShader hiZGenerateShader;

        [SerializeField]
        private ComputeShader bufferCopyShader;

        private AppContext _appContext;
        private SessionConfig _pendingSession;
        private SavedServerList _savedServerList;
        private ScreenManager _screenManager;

        private void Awake()
        {
            LoadedSettings settings = SettingsLoader.Load();
            ILogger logger = new UnityLogger();
            UserPreferences userPreferences = UserPreferences.Load();

            IFrameProfiler frameProfiler = new FrameProfiler();
            frameProfiler.Enabled = settings.Debug.EnableProfiling;
            IPipelineStats pipelineStats = new PipelineStats();
            pipelineStats.Enabled = settings.Debug.EnableProfiling;

            _screenManager = gameObject.AddComponent<ScreenManager>();
            _savedServerList = new SavedServerList();

            _appContext = new AppContext(
                settings, logger, frameProfiler, pipelineStats,
                userPreferences, _screenManager, _savedServerList, this);

            _appContext.VoxelMaterial = voxelMaterial;
            _appContext.FrustumCullShader = frustumCullShader;
            _appContext.HiZGenerateShader = hiZGenerateShader;
            _appContext.BufferCopyShader = bufferCopyShader;
        }

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
                UnityEngine.Debug.LogError($"[Lithforge] Error during shutdown save: {ex}");
            }

            session.Dispose();
            SessionOrchestrator.DisposeContentResources(session.Context.Content);
            _appContext.CurrentSession = null;
        }

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
            hostSettings.Initialize(panelSettings, _screenManager, onSessionSelected);
            _screenManager.Register(hostSettings);

            GameObject joinGameObj = new("JoinGameScreen");
            JoinGameScreen joinGame = joinGameObj.AddComponent<JoinGameScreen>();
            joinGame.Initialize(panelSettings, _screenManager, _savedServerList, clientConfig =>
            {
                _pendingSession = clientConfig;
            });
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
