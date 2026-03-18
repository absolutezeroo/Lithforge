using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.UI.Navigation;
using Lithforge.Runtime.World;

using UnityEngine;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Session
{
    /// <summary>
    ///     App-lifetime POCO holding settings, logger, profiler, and UI manager
    ///     that survive across game sessions.
    /// </summary>
    public sealed class AppContext
    {
        public AppContext(
            LoadedSettings settings,
            ILogger logger,
            IFrameProfiler frameProfiler,
            IPipelineStats pipelineStats,
            UserPreferences userPreferences,
            ScreenManager screenManager,
            SavedServerList savedServerList,
            MonoBehaviour coroutineHost)
        {
            Settings = settings;
            Logger = logger;
            FrameProfiler = frameProfiler;
            PipelineStats = pipelineStats;
            UserPreferences = userPreferences;
            ScreenManager = screenManager;
            SavedServerList = savedServerList;
            CoroutineHost = coroutineHost;
        }
        public LoadedSettings Settings { get; }

        public ILogger Logger { get; }

        public IFrameProfiler FrameProfiler { get; }

        public IPipelineStats PipelineStats { get; }

        public UserPreferences UserPreferences { get; }

        public ScreenManager ScreenManager { get; }

        public SavedServerList SavedServerList { get; }

        /// <summary>
        ///     The bootstrap MonoBehaviour, used only for StartCoroutine and
        ///     inspector-assigned references (compute shaders, materials).
        /// </summary>
        public MonoBehaviour CoroutineHost { get; }

        /// <summary>Inspector-assigned or runtime-loaded compute shaders.</summary>
        public ComputeShader FrustumCullShader { get; set; }

        public ComputeShader HiZGenerateShader { get; set; }

        public ComputeShader BufferCopyShader { get; set; }

        /// <summary>Inspector-assigned or runtime-loaded voxel material.</summary>
        public Material VoxelMaterial { get; set; }

        /// <summary>
        ///     The currently running game session, if any.
        ///     Set by <see cref="SessionOrchestrator"/> during session lifecycle.
        ///     Used by the bootstrap's OnDestroy for synchronous save fallback.
        /// </summary>
        public GameSession CurrentSession { get; set; }
    }
}
