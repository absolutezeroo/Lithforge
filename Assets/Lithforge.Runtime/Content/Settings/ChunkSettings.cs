using UnityEngine;

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

        [Tooltip("Number of chunks to load around spawn before gameplay starts")]
        [Range(1, 8)]
        [SerializeField] private int spawnLoadRadius = 2;

        [Header("Frame Budget")]
        [Tooltip("Maximum chunk generation jobs scheduled per frame")]
        [Range(1, 16)]
        [SerializeField] private int maxGenerationsPerFrame = 4;

        [Tooltip("Maximum chunk mesh jobs scheduled per frame")]
        [Range(1, 16)]
        [SerializeField] private int maxMeshesPerFrame = 4;

        [Header("Y Range — Loading")]
        [Tooltip("Minimum Y chunk offset from camera to load")]
        [SerializeField] private int yLoadMin = -1;

        [Tooltip("Maximum Y chunk offset from camera to load")]
        [SerializeField] private int yLoadMax = 3;

        [Header("Y Range — Unloading")]
        [Tooltip("Minimum Y chunk offset below which chunks are unloaded")]
        [SerializeField] private int yUnloadMin = -2;

        [Tooltip("Maximum Y chunk offset above which chunks are unloaded")]
        [SerializeField] private int yUnloadMax = 4;

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

        public int SpawnFallbackY
        {
            get { return spawnFallbackY; }
        }

        public int InitialSpawnYOffset
        {
            get { return initialSpawnYOffset; }
        }
    }
}
