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
        [SerializeField] private int poolSize = 256;

        [Header("Loading")]
        [Tooltip("Render distance in chunks")]
        [Range(1, 32)]
        [SerializeField] private int renderDistance = 4;

        [
         Tooltip("Number of chunks to load around spawn before gameplay starts")]
        [Range(1, 8)]
        [SerializeField] private int spawnLoadRadius = 2;

        [
         Header("Frame Budget")]
        [Tooltip("Maximum chunk generation jobs scheduled per frame")]
        [Range(1, 16)]
        [SerializeField] private int maxGenerationsPerFrame = 8;

        [
         Tooltip("Maximum chunk mesh jobs scheduled per frame")]
        [Range(1, 16)]
        [SerializeField] private int maxMeshesPerFrame = 8;

        [
         Tooltip("Maximum completed generation jobs processed per frame (spreads decoration + light work)")]
        [Range(1, 32)]
        [SerializeField] private int maxGenCompletionsPerFrame = 8;

        [ Tooltip("Maximum completed mesh jobs processed per frame (spreads GPU uploads)")]
        [Range(1, 32)]
        [SerializeField] private int maxMeshCompletionsPerFrame = 8;

        [ Tooltip("Maximum cross-chunk light update jobs processed per frame")]
        [Range(1, 32)]
        [SerializeField] private int maxLightUpdatesPerFrame = 6;

        [ Tooltip("Milliseconds of CPU budget for processing completed generation jobs per frame")]
        [Range(0.1f, 8f)]
        [SerializeField] private float genCompletionBudgetMs = 2f;

        [ Tooltip("Milliseconds of CPU budget for processing completed mesh jobs per frame")]
        [Range(0.1f, 8f)]
        [SerializeField] private float meshCompletionBudgetMs = 2f;

        [
         Tooltip("Milliseconds of CPU budget for processing completed LOD mesh jobs per frame")]
        [Range(0.1f, 8f)]
        [SerializeField] private float lodCompletionBudgetMs = 1f;

        [Tooltip("Maximum completed LOD mesh jobs processed per frame (secondary cap alongside ms budget)")]
        [Range(1, 32)]
        [SerializeField] private int maxLODCompletionsPerFrame = 4;

        [Header("Y Range — Loading")]
        [Tooltip("Minimum Y chunk offset from camera to load")]
        [SerializeField] private int yLoadMin = -1;

        [Tooltip("Maximum Y chunk offset from camera to load")]
        [SerializeField] private int yLoadMax = 3;

        [Header("Unloading")]
        [Tooltip("Milliseconds of CPU budget for unloading chunks per frame")]
        [Range(0.5f, 10f)]
        [SerializeField] private float unloadBudgetMs = 2f;

        [Header("Y Range — Unloading")]
        [Tooltip("Minimum Y chunk offset below which chunks are unloaded")]
        [SerializeField] private int yUnloadMin = -2;

        [Tooltip("Maximum Y chunk offset above which chunks are unloaded")]
        [SerializeField] private int yUnloadMax = 4;

        [Header("LOD")]
        [Tooltip("Chunk distance (in chunks) at which LOD1 (2x downsample) begins")]
        [Range(2, 32)]
        [SerializeField] private int lod1Distance = 4;

        [Tooltip("Chunk distance (in chunks) at which LOD2 (4x downsample) begins")]
        [Range(4, 48)]
        [SerializeField] private int lod2Distance = 8;

        [Tooltip("Chunk distance (in chunks) at which LOD3 (8x downsample) begins")]
        [Range(6, 64)]
        [SerializeField] private int lod3Distance = 14;

        [Tooltip("Maximum LOD mesh jobs scheduled per frame")]
        [Range(1, 16)]
        [SerializeField] private int maxLODMeshesPerFrame = 4;

        [Header("Spawn")]
        [Tooltip("Fallback world Y for player if no solid block is found during spawn scan")]
        [SerializeField] private int spawnFallbackY = 65;

        [Tooltip("Initial Y offset above sea level for player position before safe-spawn scan")]
        [SerializeField] private int initialSpawnYOffset = 32;

        public int PoolSize
        {
            get { return poolSize; }
        }

        public int RenderDistance
        {
            get { return renderDistance; }
        }

        public int SpawnLoadRadius
        {
            get { return spawnLoadRadius; }
        }

        public int MaxGenerationsPerFrame
        {
            get { return maxGenerationsPerFrame; }
        }

        public int MaxMeshesPerFrame
        {
            get { return maxMeshesPerFrame; }
        }

        public int MaxGenCompletionsPerFrame
        {
            get { return maxGenCompletionsPerFrame; }
        }

        public int MaxMeshCompletionsPerFrame
        {
            get { return maxMeshCompletionsPerFrame; }
        }

        public int MaxLightUpdatesPerFrame
        {
            get { return maxLightUpdatesPerFrame; }
        }

        public int YLoadMin
        {
            get { return yLoadMin; }
        }

        public int YLoadMax
        {
            get { return yLoadMax; }
        }

        public int YUnloadMin
        {
            get { return yUnloadMin; }
        }

        public int YUnloadMax
        {
            get { return yUnloadMax; }
        }

        public int LOD1Distance
        {
            get { return lod1Distance; }
        }

        public int LOD2Distance
        {
            get { return lod2Distance; }
        }

        public int LOD3Distance
        {
            get { return lod3Distance; }
        }

        public int MaxLODMeshesPerFrame
        {
            get { return maxLODMeshesPerFrame; }
        }

        public int MaxLODCompletionsPerFrame
        {
            get { return maxLODCompletionsPerFrame; }
        }

        public float GenCompletionBudgetMs
        {
            get { return genCompletionBudgetMs; }
        }

        public float MeshCompletionBudgetMs
        {
            get { return meshCompletionBudgetMs; }
        }

        public float LodCompletionBudgetMs
        {
            get { return lodCompletionBudgetMs; }
        }

        public int SpawnFallbackY
        {
            get { return spawnFallbackY; }
        }

        public int InitialSpawnYOffset
        {
            get { return initialSpawnYOffset; }
        }

        public float UnloadBudgetMs
        {
            get { return unloadBudgetMs; }
        }
    }
}
