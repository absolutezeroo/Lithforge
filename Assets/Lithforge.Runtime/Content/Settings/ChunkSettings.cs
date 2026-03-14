using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Settings
{
    [CreateAssetMenu(fileName = "ChunkSettings", menuName = "Lithforge/Settings/Chunk", order = 1)]
    public sealed class ChunkSettings : ScriptableObject
    {
        [Header("Pool")]
        [Tooltip("Number of chunk buffers in the pool")]
        [Min(16)]
        [FormerlySerializedAs("poolSize")]
        [SerializeField] private int _poolSize = 256;

        [Header("Loading")]
        [Tooltip("Render distance in chunks")]
        [Range(1, 32)]
        [FormerlySerializedAs("renderDistance")]
        [SerializeField] private int _renderDistance = 4;

        [Tooltip("Number of chunks to load around spawn before gameplay starts")]
        [Range(1, 8)]
        [FormerlySerializedAs("spawnLoadRadius")]
        [SerializeField] private int _spawnLoadRadius = 2;

        [Header("Frame Budget")]
        [Tooltip("Maximum chunk generation jobs scheduled per frame")]
        [Range(1, 16)]
        [FormerlySerializedAs("maxGenerationsPerFrame")]
        [SerializeField] private int _maxGenerationsPerFrame = 8;

        [Tooltip("Maximum chunk mesh jobs scheduled per frame")]
        [Range(1, 16)]
        [FormerlySerializedAs("maxMeshesPerFrame")]
        [SerializeField] private int _maxMeshesPerFrame = 8;

        [Tooltip("Maximum completed generation jobs processed per frame (spreads decoration + light work)")]
        [Range(1, 32)]
        [FormerlySerializedAs("maxGenCompletionsPerFrame")]
        [SerializeField] private int _maxGenCompletionsPerFrame = 8;

        [Tooltip("Maximum completed mesh jobs processed per frame (spreads GPU uploads)")]
        [Range(1, 32)]
        [FormerlySerializedAs("maxMeshCompletionsPerFrame")]
        [SerializeField] private int _maxMeshCompletionsPerFrame = 8;

        [Tooltip("Maximum cross-chunk light update jobs processed per frame")]
        [Range(1, 32)]
        [FormerlySerializedAs("maxLightUpdatesPerFrame")]
        [SerializeField] private int _maxLightUpdatesPerFrame = 6;

        [Tooltip("Milliseconds of CPU budget for processing completed generation jobs per frame")]
        [Range(0.1f, 8f)]
        [FormerlySerializedAs("genCompletionBudgetMs")]
        [SerializeField] private float _genCompletionBudgetMs = 2f;

        [Tooltip("Milliseconds of CPU budget for processing completed mesh jobs per frame")]
        [Range(0.1f, 8f)]
        [FormerlySerializedAs("meshCompletionBudgetMs")]
        [SerializeField] private float _meshCompletionBudgetMs = 2f;

        [Tooltip("Milliseconds of CPU budget for processing completed LOD mesh jobs per frame")]
        [Range(0.1f, 8f)]
        [FormerlySerializedAs("lodCompletionBudgetMs")]
        [SerializeField] private float _lodCompletionBudgetMs = 1f;

        [Tooltip("Maximum completed LOD mesh jobs processed per frame (secondary cap alongside ms budget)")]
        [Range(1, 32)]
        [FormerlySerializedAs("maxLODCompletionsPerFrame")]
        [SerializeField] private int _maxLODCompletionsPerFrame = 4;

        [Header("Y Range — Loading")]
        [Tooltip("Minimum Y chunk offset from camera to load")]
        [FormerlySerializedAs("yLoadMin")]
        [SerializeField] private int _yLoadMin = -1;

        [Tooltip("Maximum Y chunk offset from camera to load")]
        [FormerlySerializedAs("yLoadMax")]
        [SerializeField] private int _yLoadMax = 3;

        [Header("Y Range — Unloading")]
        [Tooltip("Minimum Y chunk offset below which chunks are unloaded")]
        [FormerlySerializedAs("yUnloadMin")]
        [SerializeField] private int _yUnloadMin = -2;

        [Tooltip("Maximum Y chunk offset above which chunks are unloaded")]
        [FormerlySerializedAs("yUnloadMax")]
        [SerializeField] private int _yUnloadMax = 4;

        [Header("LOD")]
        [Tooltip("Chunk distance (in chunks) at which LOD1 (2x downsample) begins")]
        [Range(2, 32)]
        [FormerlySerializedAs("lod1Distance")]
        [SerializeField] private int _lod1Distance = 4;

        [Tooltip("Chunk distance (in chunks) at which LOD2 (4x downsample) begins")]
        [Range(4, 48)]
        [FormerlySerializedAs("lod2Distance")]
        [SerializeField] private int _lod2Distance = 8;

        [Tooltip("Chunk distance (in chunks) at which LOD3 (8x downsample) begins")]
        [Range(6, 64)]
        [FormerlySerializedAs("lod3Distance")]
        [SerializeField] private int _lod3Distance = 14;

        [Tooltip("Maximum LOD mesh jobs scheduled per frame")]
        [Range(1, 16)]
        [FormerlySerializedAs("maxLODMeshesPerFrame")]
        [SerializeField] private int _maxLODMeshesPerFrame = 4;

        [Header("Spawn")]
        [Tooltip("Fallback world Y for player if no solid block is found during spawn scan")]
        [FormerlySerializedAs("spawnFallbackY")]
        [SerializeField] private int _spawnFallbackY = 65;

        [Tooltip("Initial Y offset above sea level for player position before safe-spawn scan")]
        [FormerlySerializedAs("initialSpawnYOffset")]
        [SerializeField] private int _initialSpawnYOffset = 32;

        public int PoolSize
        {
            get { return _poolSize; }
        }

        public int RenderDistance
        {
            get { return _renderDistance; }
        }

        public int SpawnLoadRadius
        {
            get { return _spawnLoadRadius; }
        }

        public int MaxGenerationsPerFrame
        {
            get { return _maxGenerationsPerFrame; }
        }

        public int MaxMeshesPerFrame
        {
            get { return _maxMeshesPerFrame; }
        }

        public int MaxGenCompletionsPerFrame
        {
            get { return _maxGenCompletionsPerFrame; }
        }

        public int MaxMeshCompletionsPerFrame
        {
            get { return _maxMeshCompletionsPerFrame; }
        }

        public int MaxLightUpdatesPerFrame
        {
            get { return _maxLightUpdatesPerFrame; }
        }

        public int YLoadMin
        {
            get { return _yLoadMin; }
        }

        public int YLoadMax
        {
            get { return _yLoadMax; }
        }

        public int YUnloadMin
        {
            get { return _yUnloadMin; }
        }

        public int YUnloadMax
        {
            get { return _yUnloadMax; }
        }

        public int LOD1Distance
        {
            get { return _lod1Distance; }
        }

        public int LOD2Distance
        {
            get { return _lod2Distance; }
        }

        public int LOD3Distance
        {
            get { return _lod3Distance; }
        }

        public int MaxLODMeshesPerFrame
        {
            get { return _maxLODMeshesPerFrame; }
        }

        public int MaxLODCompletionsPerFrame
        {
            get { return _maxLODCompletionsPerFrame; }
        }

        public float GenCompletionBudgetMs
        {
            get { return _genCompletionBudgetMs; }
        }

        public float MeshCompletionBudgetMs
        {
            get { return _meshCompletionBudgetMs; }
        }

        public float LodCompletionBudgetMs
        {
            get { return _lodCompletionBudgetMs; }
        }

        public int SpawnFallbackY
        {
            get { return _spawnFallbackY; }
        }

        public int InitialSpawnYOffset
        {
            get { return _initialSpawnYOffset; }
        }
    }
}
