using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    [CreateAssetMenu(fileName = "ChunkSettings", menuName = "Lithforge/Settings/Chunk", order = 1)]
    public sealed class ChunkSettings : ScriptableObject
    {
        [Header("Pool")]
        [Tooltip("Number of chunk buffers in the pool")]
        [Min(16)]
        [SerializeField] private int _poolSize = 256;

        [Header("Loading")]
        [Tooltip("Render distance in chunks")]
        [Range(1, 32)]
        [SerializeField] private int _renderDistance = 4;

        [Tooltip("Number of chunks to load around spawn before gameplay starts")]
        [Range(1, 8)]
        [SerializeField] private int _spawnLoadRadius = 2;

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
    }
}
