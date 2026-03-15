using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Settings
{
    /// <summary>
    /// Controls chunk loading/unloading distances, per-frame budgets, LOD thresholds, and spawn behavior.
    /// </summary>
    /// <remarks>
    /// Several scheduling parameters (generation/mesh counts, LOD distances) are deprecated and now
    /// derived from <see cref="RenderDistance"/> at runtime via <c>SchedulingConfig</c>.
    /// Loaded from <c>Resources/Settings/ChunkSettings</c>.
    /// </remarks>
    [CreateAssetMenu(fileName = "ChunkSettings", menuName = "Lithforge/Settings/Chunk", order = 1)]
    public sealed class ChunkSettings : ScriptableObject
    {
        /// <summary>How many NativeArray chunk buffers to pre-allocate in the pool.</summary>
        [Header("Pool")]
        [Tooltip("Number of chunk buffers in the pool")]
        [Min(16)]
        [SerializeField] private int poolSize = 256;

        /// <summary>How far the player can see, in chunk units (Chebyshev XZ distance).</summary>
        [Header("Loading")]
        [Tooltip("Render distance in chunks")]
        [Range(1, 32)]
        [SerializeField] private int renderDistance = 4;

        /// <summary>Chunk radius around spawn that must finish loading before the loading screen clears.</summary>
        [
         Tooltip("Number of chunks to load around spawn before gameplay starts")]
        [Range(1, 8)]
        [SerializeField] private int spawnLoadRadius = 2;

        /// <summary>Deprecated: now derived from renderDistance via SchedulingConfig.</summary>
        [Header("Frame Budget")]
        [HideInInspector]
        [SerializeField] private int maxGenerationsPerFrame = 8;

        /// <summary>Deprecated: now derived from renderDistance via SchedulingConfig.</summary>
        [HideInInspector]
        [SerializeField] private int maxMeshesPerFrame = 8;

        /// <summary>Deprecated: now derived from renderDistance via SchedulingConfig.</summary>
        [HideInInspector]
        [SerializeField] private int maxGenCompletionsPerFrame = 8;

        /// <summary>Deprecated: now derived from renderDistance via SchedulingConfig.</summary>
        [HideInInspector]
        [SerializeField] private int maxMeshCompletionsPerFrame = 8;

        /// <summary>Circuit-breaker cap on cross-chunk light propagation jobs dispatched each frame.</summary>
        [ Tooltip("Maximum cross-chunk light update jobs processed per frame")]
        [Range(1, 32)]
        [SerializeField] private int maxLightUpdatesPerFrame = 6;

        /// <summary>Wall-clock milliseconds the main thread may spend completing generation results each frame.</summary>
        [ Tooltip("Milliseconds of CPU budget for processing completed generation jobs per frame")]
        [Range(0.1f, 8f)]
        [SerializeField] private float genCompletionBudgetMs = 2f;

        /// <summary>Wall-clock milliseconds the main thread may spend uploading completed mesh data each frame.</summary>
        [ Tooltip("Milliseconds of CPU budget for processing completed mesh jobs per frame")]
        [Range(0.1f, 8f)]
        [SerializeField] private float meshCompletionBudgetMs = 2f;

        /// <summary>Wall-clock milliseconds the main thread may spend uploading completed LOD mesh data each frame.</summary>
        [
         Tooltip("Milliseconds of CPU budget for processing completed LOD mesh jobs per frame")]
        [Range(0.1f, 8f)]
        [SerializeField] private float lodCompletionBudgetMs = 1f;

        /// <summary>Deprecated: now derived from renderDistance via SchedulingConfig.</summary>
        [HideInInspector]
        [SerializeField] private int maxLODCompletionsPerFrame = 4;

        /// <summary>Lowest Y chunk offset (relative to camera chunk) that the loading scheduler will request.</summary>
        [Header("Y Range — Loading")]
        [Tooltip("Minimum Y chunk offset from camera to load")]
        [SerializeField] private int yLoadMin = -1;

        /// <summary>Highest Y chunk offset (relative to camera chunk) that the loading scheduler will request.</summary>
        [Tooltip("Maximum Y chunk offset from camera to load")]
        [SerializeField] private int yLoadMax = 3;

        /// <summary>Wall-clock milliseconds the main thread may spend disposing out-of-range chunks each frame.</summary>
        [Header("Unloading")]
        [Tooltip("Milliseconds of CPU budget for unloading chunks per frame")]
        [Range(0.5f, 10f)]
        [SerializeField] private float unloadBudgetMs = 2f;

        /// <summary>Chunks below this Y offset from the camera chunk are eligible for unloading.</summary>
        [Header("Y Range — Unloading")]
        [Tooltip("Minimum Y chunk offset below which chunks are unloaded")]
        [SerializeField] private int yUnloadMin = -2;

        /// <summary>Chunks above this Y offset from the camera chunk are eligible for unloading.</summary>
        [Tooltip("Maximum Y chunk offset above which chunks are unloaded")]
        [SerializeField] private int yUnloadMax = 4;

        /// <summary>Deprecated: now derived from renderDistance via SchedulingConfig.</summary>
        [Header("LOD")]
        [HideInInspector]
        [SerializeField] private int lod1Distance = 4;

        /// <summary>Deprecated: now derived from renderDistance via SchedulingConfig.</summary>
        [HideInInspector]
        [SerializeField] private int lod2Distance = 8;

        /// <summary>Deprecated: now derived from renderDistance via SchedulingConfig.</summary>
        [HideInInspector]
        [SerializeField] private int lod3Distance = 14;

        /// <summary>Deprecated: now derived from renderDistance via SchedulingConfig.</summary>
        [HideInInspector]
        [SerializeField] private int maxLODMeshesPerFrame = 4;

        /// <summary>World Y coordinate used when the safe-spawn column scan finds no solid ground.</summary>
        [Header("Spawn")]
        [Tooltip("Fallback world Y for player if no solid block is found during spawn scan")]
        [SerializeField] private int spawnFallbackY = 65;

        /// <summary>Blocks above sea level to start the downward safe-spawn scan from.</summary>
        [Tooltip("Initial Y offset above sea level for player position before safe-spawn scan")]
        [SerializeField] private int initialSpawnYOffset = 32;

        /// <inheritdoc cref="poolSize"/>
        public int PoolSize
        {
            get { return poolSize; }
        }

        /// <inheritdoc cref="renderDistance"/>
        public int RenderDistance
        {
            get { return renderDistance; }
        }

        /// <inheritdoc cref="spawnLoadRadius"/>
        public int SpawnLoadRadius
        {
            get { return spawnLoadRadius; }
        }

        /// <inheritdoc cref="maxLightUpdatesPerFrame"/>
        public int MaxLightUpdatesPerFrame
        {
            get { return maxLightUpdatesPerFrame; }
        }

        /// <inheritdoc cref="yLoadMin"/>
        public int YLoadMin
        {
            get { return yLoadMin; }
        }

        /// <inheritdoc cref="yLoadMax"/>
        public int YLoadMax
        {
            get { return yLoadMax; }
        }

        /// <inheritdoc cref="yUnloadMin"/>
        public int YUnloadMin
        {
            get { return yUnloadMin; }
        }

        /// <inheritdoc cref="yUnloadMax"/>
        public int YUnloadMax
        {
            get { return yUnloadMax; }
        }

        /// <inheritdoc cref="genCompletionBudgetMs"/>
        public float GenCompletionBudgetMs
        {
            get { return genCompletionBudgetMs; }
        }

        /// <inheritdoc cref="meshCompletionBudgetMs"/>
        public float MeshCompletionBudgetMs
        {
            get { return meshCompletionBudgetMs; }
        }

        /// <inheritdoc cref="lodCompletionBudgetMs"/>
        public float LodCompletionBudgetMs
        {
            get { return lodCompletionBudgetMs; }
        }

        /// <inheritdoc cref="spawnFallbackY"/>
        public int SpawnFallbackY
        {
            get { return spawnFallbackY; }
        }

        /// <inheritdoc cref="initialSpawnYOffset"/>
        public int InitialSpawnYOffset
        {
            get { return initialSpawnYOffset; }
        }

        /// <inheritdoc cref="unloadBudgetMs"/>
        public float UnloadBudgetMs
        {
            get { return unloadBudgetMs; }
        }
    }
}
