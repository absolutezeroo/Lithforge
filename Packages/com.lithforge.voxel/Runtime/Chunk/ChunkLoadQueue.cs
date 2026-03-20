using System;
using System.Collections.Generic;

using Unity.Mathematics;

namespace Lithforge.Voxel.Chunk
{
    /// <summary>
    ///     Manages the chunk loading queue: rebuilding the queue from player interest regions,
    ///     forward-weighted Schwartzian sorting, reference counting, and unload candidate collection.
    ///     Internal helper owned by <see cref="ChunkManager" />.
    /// </summary>
    internal sealed class ChunkLoadQueue
    {
        /// <summary>Ordered list of chunk coordinates to load, rebuilt by UpdateLoadingQueue.</summary>
        private readonly List<int3> _loadQueue = new();

        /// <summary>
        ///     Cursor index into _loadQueue. Advanced instead of RemoveRange(0, count).
        ///     Reset when UpdateLoadingQueue() rebuilds the queue.
        /// </summary>
        private int _loadQueueIndex;

        /// <summary>Deduplication set for the union load queue rebuild. Cleared after each use.</summary>
        private readonly HashSet<int3> _loadQueueSet = new();

        /// <summary>Last per-player chunk coordinates used for rebuild-threshold tracking.</summary>
        private readonly List<int3> _lastPlayerChunkCoords = new();

        /// <summary>
        ///     Schwartzian transform caches for forward-weighted queue sort.
        ///     Resized on demand, never shrunk. Owner: ChunkLoadQueue.
        /// </summary>
        private float[] _sortScoreCache = Array.Empty<float>();

        /// <summary>Parallel coordinate array for Schwartzian sort in UpdateLoadingQueue.</summary>
        private int3[] _sortCoordCache = Array.Empty<int3>();

        /// <summary>
        ///     Schwartzian transform caches for distance-sorted unload.
        ///     Resized on demand, never shrunk. Owner: ChunkLoadQueue.
        /// </summary>
        private int[] _unloadDistCache = Array.Empty<int>();

        /// <summary>Parallel coordinate array for Schwartzian sort in UnloadDistantChunks.</summary>
        private int3[] _unloadCoordCache = Array.Empty<int3>();

        /// <summary>Reusable list for collecting unload candidates in UnloadDistantChunks.</summary>
        private readonly List<int3> _toRemoveCache = new();

        /// <summary>Maximum chunk Y coordinate (inclusive) for loading.</summary>
        private readonly int _yLoadMax;

        /// <summary>Minimum chunk Y coordinate (inclusive) for loading.</summary>
        private readonly int _yLoadMin;

        /// <summary>Maximum chunk Y coordinate (inclusive) for unloading threshold.</summary>
        private readonly int _yUnloadMax;

        /// <summary>Minimum chunk Y coordinate (inclusive) for unloading threshold.</summary>
        private readonly int _yUnloadMin;

        /// <summary>Current render distance in chunks (Chebyshev XZ radius).</summary>
        internal int RenderDistance { get; private set; }

        /// <summary>Number of remaining entries in the load queue.</summary>
        internal int PendingLoadCount
        {
            get { return _loadQueue.Count - _loadQueueIndex; }
        }

        /// <summary>Read-only access to the last known player chunk coordinates.</summary>
        internal IReadOnlyList<int3> LastPlayerChunkCoords
        {
            get { return _lastPlayerChunkCoords; }
        }

        /// <summary>Creates a ChunkLoadQueue with the given render distance and Y load/unload bounds.</summary>
        internal ChunkLoadQueue(
            int renderDistance,
            int yLoadMin,
            int yLoadMax,
            int yUnloadMin,
            int yUnloadMax)
        {
            RenderDistance = renderDistance;
            _yLoadMin = yLoadMin;
            _yLoadMax = yLoadMax;
            _yUnloadMin = yUnloadMin;
            _yUnloadMax = yUnloadMax;
        }

        /// <summary>Updates the render distance (clamped to at least 1).</summary>
        internal void SetRenderDistance(int distance)
        {
            RenderDistance = math.max(1, distance);
        }

        /// <summary>
        ///     Returns the first player chunk coordinate from the last UpdateLoadingQueue call.
        ///     Used by LiquidScheduler for determining liquid simulation radius.
        ///     Returns int3.zero if no player positions are available.
        /// </summary>
        internal int3 GetCameraChunkCoord()
        {
            if (_lastPlayerChunkCoords.Count > 0)
            {
                return _lastPlayerChunkCoords[0];
            }

            return int3.zero;
        }

        /// <summary>
        ///     Rebuilds the loading queue as the union of all player interest regions,
        ///     sorted by minimum forward-weighted distance to any player origin.
        ///     Only rebuilds if any origin moved far enough or the queue is empty.
        /// </summary>
        internal void UpdateLoadingQueue(
            ReadOnlySpan<int3> playerChunkCoords,
            float3 cameraForward,
            Dictionary<int3, ManagedChunk> chunks)
        {
            if (playerChunkCoords.Length == 0)
            {
                return;
            }

            // Detect if any player moved >= 2 chunks since last rebuild
            bool anyMoved = playerChunkCoords.Length != _lastPlayerChunkCoords.Count;

            if (!anyMoved)
            {
                for (int p = 0; p < playerChunkCoords.Length; p++)
                {
                    int3 diff = playerChunkCoords[p] - _lastPlayerChunkCoords[p];
                    int moved = math.max(math.abs(diff.x), math.max(math.abs(diff.y), math.abs(diff.z)));

                    if (moved >= 2)
                    {
                        anyMoved = true;
                        break;
                    }
                }
            }

            bool queueHasWork = _loadQueue.Count - _loadQueueIndex > 0;

            if (queueHasWork && !anyMoved)
            {
                return;
            }

            // Sync last-known positions
            _lastPlayerChunkCoords.Clear();

            for (int p = 0; p < playerChunkCoords.Length; p++)
            {
                _lastPlayerChunkCoords.Add(playerChunkCoords[p]);
            }

            _loadQueue.Clear();
            _loadQueueIndex = 0;

            // Union of all player interest regions, deduplicated
            for (int p = 0; p < playerChunkCoords.Length; p++)
            {
                int3 origin = playerChunkCoords[p];

                for (int d = 0; d <= RenderDistance; d++)
                {
                    for (int x = -d; x <= d; x++)
                    {
                        for (int z = -d; z <= d; z++)
                        {
                            if (math.abs(x) != d && math.abs(z) != d)
                            {
                                continue;
                            }

                            for (int y = _yLoadMin; y <= _yLoadMax; y++)
                            {
                                int3 coord = origin + new int3(x, y, z);

                                if (!chunks.ContainsKey(coord) && _loadQueueSet.Add(coord))
                                {
                                    _loadQueue.Add(coord);
                                }
                            }
                        }
                    }
                }
            }

            _loadQueueSet.Clear();

            // Schwartzian transform: score = min over all players of (dist² * (2 - dot)),
            // so the closest chunk to any player in the forward direction scores lowest.
            int count = _loadQueue.Count;

            if (_sortScoreCache.Length < count)
            {
                int newSize = math.max(count, _sortScoreCache.Length * 2);
                _sortScoreCache = new float[newSize];
                _sortCoordCache = new int3[newSize];
            }

            float3 forwardXZ = math.normalizesafe(new float3(cameraForward.x, 0, cameraForward.z));

            for (int i = 0; i < count; i++)
            {
                int3 coord = _loadQueue[i];
                _sortCoordCache[i] = coord;
                float bestScore = float.MaxValue;

                for (int p = 0; p < playerChunkCoords.Length; p++)
                {
                    int3 delta = coord - playerChunkCoords[p];
                    float3 dirXZ = math.normalizesafe(new float3(delta.x, 0, delta.z));
                    float dot = math.dot(dirXZ, forwardXZ);
                    float distSq = math.lengthsq((float3)delta);
                    float score = distSq * (2.0f - dot);

                    if (score < bestScore)
                    {
                        bestScore = score;
                    }
                }

                _sortScoreCache[i] = bestScore;
            }

            Array.Sort(_sortScoreCache, _sortCoordCache, 0, count);

            _loadQueue.Clear();

            for (int i = 0; i < count; i++)
            {
                _loadQueue.Add(_sortCoordCache[i]);
            }
        }

        /// <summary>
        ///     Single-player backward-compatible wrapper for
        ///     <see cref="UpdateLoadingQueue(ReadOnlySpan{int3}, float3, Dictionary{int3, ManagedChunk})" />.
        /// </summary>
        internal void UpdateLoadingQueue(
            int3 cameraChunkCoord,
            float3 cameraForward,
            Dictionary<int3, ManagedChunk> chunks)
        {
            Span<int3> single = stackalloc int3[1];
            single[0] = cameraChunkCoord;
            UpdateLoadingQueue((ReadOnlySpan<int3>)single, cameraForward, chunks);
        }

        /// <summary>
        ///     Recomputes per-chunk reference counts from the current set of player chunk origins.
        ///     For each loaded chunk: counts how many players have it within their interest sphere.
        ///     When RefCount drops from positive to zero, sets the grace period expiry timestamp.
        ///     Called after UpdateLoadingQueue with the same player coordinates.
        /// </summary>
        internal void AdjustRefCounts(
            ReadOnlySpan<int3> playerChunkCoords,
            double currentRealtime,
            double gracePeriodSeconds,
            Dictionary<int3, ManagedChunk> chunks)
        {
            foreach (KeyValuePair<int3, ManagedChunk> kvp in chunks)
            {
                ManagedChunk chunk = kvp.Value;
                int3 coord = kvp.Key;
                int newCount = 0;

                for (int p = 0; p < playerChunkCoords.Length; p++)
                {
                    int3 diff = coord - playerChunkCoords[p];
                    int xzDist = math.max(math.abs(diff.x), math.abs(diff.z));
                    bool yInRange = diff.y >= _yLoadMin && diff.y <= _yLoadMax;

                    if (xzDist <= RenderDistance && yInRange)
                    {
                        newCount++;
                    }
                }

                int oldCount = chunk.RefCount;
                chunk.RefCount = newCount;

                if (newCount == 0 && (oldCount > 0 || chunk.GracePeriodExpiry == double.MaxValue))
                {
                    // Player(s) left range, or chunk was never referenced: arm grace period
                    chunk.GracePeriodExpiry = currentRealtime + gracePeriodSeconds;
                }
            }
        }

        /// <summary>
        ///     Pops up to maxCount entries from the load queue. Returns coordinates for
        ///     chunks that need to be created. Skips already-loaded chunks without consuming budget.
        ///     The ChunkManager uses the returned coordinates to checkout from the pool and create chunks.
        /// </summary>
        internal void FillCoordsToGenerate(
            List<int3> result,
            int maxCount,
            Dictionary<int3, ManagedChunk> chunks)
        {
            result.Clear();

            while (_loadQueueIndex < _loadQueue.Count && result.Count < maxCount)
            {
                int3 coord = _loadQueue[_loadQueueIndex];
                _loadQueueIndex++;

                // Skip already-loaded chunks without consuming budget
                if (chunks.ContainsKey(coord))
                {
                    continue;
                }

                result.Add(coord);
            }

            // If cursor has consumed the entire queue, clear to free memory
            if (_loadQueueIndex >= _loadQueue.Count)
            {
                _loadQueue.Clear();
                _loadQueueIndex = 0;
            }
        }

        /// <summary>
        ///     Collects chunk coordinates eligible for unloading: zero-refcount chunks past
        ///     their grace period and outside the player interest region. Results are sorted
        ///     by minimum Chebyshev XZ distance to nearest player (ascending, so caller can
        ///     iterate in reverse for farthest-first).
        /// </summary>
        internal void CollectUnloadCandidates(
            ReadOnlySpan<int3> playerChunkCoords,
            double currentRealtime,
            Dictionary<int3, ManagedChunk> chunks,
            List<int3> sortedCandidates)
        {
            sortedCandidates.Clear();
            _toRemoveCache.Clear();

            // Phase 1: Collect candidates — zero-refcount chunks past grace period
            foreach (KeyValuePair<int3, ManagedChunk> kvp in chunks)
            {
                ManagedChunk chunk = kvp.Value;

                // Skip chunks still referenced by a player
                if (chunk.RefCount > 0)
                {
                    continue;
                }

                // Skip chunks within grace period
                if (currentRealtime < chunk.GracePeriodExpiry)
                {
                    continue;
                }

                // No players connected: all zero-refcount past-grace chunks are candidates
                if (playerChunkCoords.Length == 0)
                {
                    _toRemoveCache.Add(kvp.Key);
                    continue;
                }

                // Check Y bounds against all player origins
                bool yInRange = false;

                for (int p = 0; p < playerChunkCoords.Length; p++)
                {
                    int3 diff = kvp.Key - playerChunkCoords[p];

                    if (diff.y >= _yUnloadMin && diff.y <= _yUnloadMax)
                    {
                        yInRange = true;
                        break;
                    }
                }

                if (!yInRange)
                {
                    _toRemoveCache.Add(kvp.Key);
                    continue;
                }

                // Check XZ distance against all player origins
                bool anyNear = false;

                for (int p = 0; p < playerChunkCoords.Length; p++)
                {
                    int3 diff = kvp.Key - playerChunkCoords[p];
                    int xzDist = math.max(math.abs(diff.x), math.abs(diff.z));

                    if (xzDist <= RenderDistance + 1)
                    {
                        anyNear = true;
                        break;
                    }
                }

                if (!anyNear)
                {
                    _toRemoveCache.Add(kvp.Key);
                }
            }

            int candidateCount = _toRemoveCache.Count;

            if (candidateCount == 0)
            {
                return;
            }

            // Phase 2: Schwartzian sort by min Chebyshev XZ distance to nearest player (ascending)
            if (_unloadDistCache.Length < candidateCount)
            {
                int newSize = math.max(candidateCount, _unloadDistCache.Length * 2);
                _unloadDistCache = new int[newSize];
                _unloadCoordCache = new int3[newSize];
            }

            for (int i = 0; i < candidateCount; i++)
            {
                int3 coord = _toRemoveCache[i];
                _unloadCoordCache[i] = coord;
                int minDist = int.MaxValue;

                for (int p = 0; p < playerChunkCoords.Length; p++)
                {
                    int3 diff = coord - playerChunkCoords[p];
                    int xzDist = math.max(math.abs(diff.x), math.abs(diff.z));

                    if (xzDist < minDist)
                    {
                        minDist = xzDist;
                    }
                }

                _unloadDistCache[i] = minDist;
            }

            Array.Sort(_unloadDistCache, _unloadCoordCache, 0, candidateCount);

            for (int i = 0; i < candidateCount; i++)
            {
                sortedCandidates.Add(_unloadCoordCache[i]);
            }
        }
    }
}
