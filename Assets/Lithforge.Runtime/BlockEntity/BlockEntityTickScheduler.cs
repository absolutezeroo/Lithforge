using System.Collections.Generic;

using Lithforge.Voxel.Block;
using Lithforge.Voxel.BlockEntity;
using Lithforge.Voxel.Chunk;

using Unity.Mathematics;

using UnityEngine.Profiling;

namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    ///     20-bucket round-robin tick scheduler for block entities.
    ///     Each frame, one bucket is ticked. Entities in the ticked bucket
    ///     receive deltaTime * BucketCount so their effective tick rate matches real time.
    /// </summary>
    public sealed class BlockEntityTickScheduler
    {
        private const int BucketCount = 20;

        private static readonly Stack<List<EntityKey>> s_listPool = new();

        private readonly List<EntityKey>[] _buckets;

        private readonly Dictionary<int3, List<EntityKey>> _chunkIndex = new();

        private readonly ChunkManager _chunkManager;

        private readonly Dictionary<EntityKey, BlockEntity> _entities = new();

        private readonly BlockEntityRegistry _registry;

        private readonly StateRegistry _stateRegistry;

        private int _currentBucket;

        public BlockEntityTickScheduler(
            ChunkManager chunkManager,
            BlockEntityRegistry registry,
            StateRegistry stateRegistry)
        {
            _chunkManager = chunkManager;
            _registry = registry;
            _stateRegistry = stateRegistry;

            _buckets = new List<EntityKey>[BucketCount];

            for (int i = 0; i < BucketCount; i++)
            {
                _buckets[i] = new List<EntityKey>();
            }

            // Wire delegate hooks on ChunkManager
            _chunkManager.OnBlockEntityPlaced += OnBlockEntityPlaced;
            _chunkManager.OnBlockEntityRemoved += OnBlockEntityRemoved;
        }

        public int EntityCount
        {
            get { return _entities.Count; }
        }

        /// <summary>
        ///     Ticks one bucket per frame. Called from GameLoop.Update().
        /// </summary>
        public void Tick(float deltaTime)
        {
            Profiler.BeginSample("BE.Tick");

            List<EntityKey> bucket = _buckets[_currentBucket];
            float effectiveDt = deltaTime * BucketCount;

            for (int i = bucket.Count - 1; i >= 0; i--)
            {
                EntityKey key = bucket[i];

                if (_entities.TryGetValue(key, out BlockEntity entity))
                {
                    entity.Tick(effectiveDt);
                }
                else
                {
                    // Entity was removed — clean up stale bucket entry
                    bucket.RemoveAt(i);
                }
            }

            _currentBucket = (_currentBucket + 1) % BucketCount;

            Profiler.EndSample();
        }

        /// <summary>
        ///     Registers all block entities from a freshly loaded/deserialized chunk.
        ///     Called by GenerationScheduler after chunk reaches Generated state with entities.
        /// </summary>
        public void RegisterEntitiesForChunk(int3 chunkCoord, ManagedChunk chunk)
        {
            if (chunk.BlockEntities == null)
            {
                return;
            }

            foreach (KeyValuePair<int, IBlockEntity> kvp in chunk.BlockEntities)
            {
                if (kvp.Value is BlockEntity runtimeEntity)
                {
                    EntityKey key = new(chunkCoord, kvp.Key);

                    if (!_entities.ContainsKey(key))
                    {
                        _entities[key] = runtimeEntity;
                        int bucketIndex = GetBucketIndex(key);
                        _buckets[bucketIndex].Add(key);

                        if (!_chunkIndex.TryGetValue(chunkCoord, out List<EntityKey> chunkList))
                        {
                            chunkList = RentList();
                            _chunkIndex[chunkCoord] = chunkList;
                        }
                        chunkList.Add(key);
                    }
                }
            }
        }

        /// <summary>
        ///     Removes all entities for a chunk being unloaded.
        ///     Called from GameLoop unload loop. Accepts null chunk — uses
        ///     internal entity tracking for cleanup.
        /// </summary>
        public void OnChunkUnloaded(int3 chunkCoord)
        {
            if (!_chunkIndex.TryGetValue(chunkCoord, out List<EntityKey> chunkList))
            {
                return;
            }

            for (int i = 0; i < chunkList.Count; i++)
            {
                _entities.Remove(chunkList[i]);
                // Bucket entries cleaned up lazily in Tick()
            }

            _chunkIndex.Remove(chunkCoord);
            ReturnList(chunkList);
        }

        private void OnBlockEntityPlaced(int3 chunkCoord, int flatIndex, StateId stateId)
        {
            ManagedChunk chunk = _chunkManager.GetChunk(chunkCoord);

            if (chunk == null)
            {
                return;
            }

            // Find the block entity type for this state
            StateRegistryEntry entry = _stateRegistry.GetEntryForState(stateId);

            if (entry == null || string.IsNullOrEmpty(entry.BlockEntityTypeId))
            {
                return;
            }

            IBlockEntity entity = _registry.CreateEntity(entry.BlockEntityTypeId);

            if (entity == null)
            {
                return;
            }

            Dictionary<int, IBlockEntity> entities = chunk.GetOrCreateBlockEntities();
            entities[flatIndex] = entity;

            if (entity is BlockEntity runtimeEntity)
            {
                EntityKey key = new(chunkCoord, flatIndex);
                _entities[key] = runtimeEntity;
                int bucketIndex = GetBucketIndex(key);
                _buckets[bucketIndex].Add(key);

                if (!_chunkIndex.TryGetValue(chunkCoord, out List<EntityKey> chunkList))
                {
                    chunkList = RentList();
                    _chunkIndex[chunkCoord] = chunkList;
                }
                chunkList.Add(key);
            }
        }

        private void OnBlockEntityRemoved(int3 chunkCoord, int flatIndex, StateId oldStateId)
        {
            ManagedChunk chunk = _chunkManager.GetChunk(chunkCoord);

            if (chunk == null || chunk.BlockEntities == null)
            {
                return;
            }

            if (chunk.BlockEntities.TryGetValue(flatIndex, out IBlockEntity entity))
            {
                entity.OnChunkUnload();
                chunk.BlockEntities.Remove(flatIndex);

                EntityKey key = new(chunkCoord, flatIndex);
                _entities.Remove(key);
                // Bucket entries cleaned up lazily in Tick()

                if (_chunkIndex.TryGetValue(chunkCoord, out List<EntityKey> chunkList))
                {
                    chunkList.Remove(key);
                    if (chunkList.Count == 0)
                    {
                        _chunkIndex.Remove(chunkCoord);
                        ReturnList(chunkList);
                    }
                }
            }
        }

        /// <summary>
        ///     Gets the block entity at the given position, or null.
        ///     Used by BlockInteraction for right-click dispatch.
        /// </summary>
        public BlockEntity GetEntity(int3 chunkCoord, int flatIndex)
        {
            EntityKey key = new(chunkCoord, flatIndex);
            _entities.TryGetValue(key, out BlockEntity entity);

            return entity;
        }

        private static List<EntityKey> RentList()
        {
            if (s_listPool.Count > 0)
            {
                List<EntityKey> list = s_listPool.Pop();
                list.Clear();
                return list;
            }
            return new List<EntityKey>();
        }

        private static void ReturnList(List<EntityKey> list)
        {
            list.Clear();
            s_listPool.Push(list);
        }

        private static int GetBucketIndex(EntityKey key)
        {
            int hash = key.GetHashCode();

            // Ensure positive modulo
            return (hash % BucketCount + BucketCount) % BucketCount;
        }
    }
}
