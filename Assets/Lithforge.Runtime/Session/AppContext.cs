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
        /// <summary>
        ///     Creates an AppContext with all app-lifetime dependencies.
        /// </summary>
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
        /// <summary>All loaded ScriptableObject settings (chunk, worldgen, rendering, etc.).</summary>
        public LoadedSettings Settings { get; }

        /// <summary>Application-wide logger implementation.</summary>
        public ILogger Logger { get; }

        /// <summary>Zero-alloc frame section profiler for timing measurements.</summary>
        public IFrameProfiler FrameProfiler { get; }

        /// <summary>Pipeline counters tracking per-frame and cumulative statistics.</summary>
        public IPipelineStats PipelineStats { get; }

        /// <summary>Persisted user preferences (volume, render distance, keybinds, etc.).</summary>
        public UserPreferences UserPreferences { get; }

        /// <summary>UI screen stack manager for modal screen navigation.</summary>
        public ScreenManager ScreenManager { get; }

        /// <summary>Persisted list of saved multiplayer server entries.</summary>
        public SavedServerList SavedServerList { get; }

        /// <summary>
        ///     The bootstrap MonoBehaviour, used only for StartCoroutine and
        ///     inspector-assigned references (compute shaders, materials).
        /// </summary>
        public MonoBehaviour CoroutineHost { get; }

        /// <summary>Inspector-assigned or runtime-loaded compute shaders.</summary>
        public ComputeShader FrustumCullShader { get; set; }

        /// <summary>Compute shader that generates the Hi-Z occlusion mipmap pyramid.</summary>
        public ComputeShader HiZGenerateShader { get; set; }

        /// <summary>Compute shader for GPU buffer copy operations during resize.</summary>
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
