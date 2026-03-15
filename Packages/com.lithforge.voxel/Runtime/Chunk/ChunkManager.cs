using System;
using System.Collections.Generic;
using System.Diagnostics;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.BlockEntity;
using Lithforge.Voxel.Storage;
using Unity.Collections;
using Unity.Mathematics;

namespace Lithforge.Voxel.Chunk
{
    public sealed class ChunkManager : IDisposable
    {
        private readonly Dictionary<int3, ManagedChunk> _chunks = new Dictionary<int3, ManagedChunk>();
        private readonly ChunkPool _pool;
        private int _renderDistance;
        private readonly int _yLoadMin;
        private readonly int _yLoadMax;
        private readonly int _yUnloadMin;
        private readonly int _yUnloadMax;
        private readonly List<int3> _loadQueue = new List<int3>();
        private bool _disposed;
        private readonly List<ManagedChunk> _meshCandidateCache = new List<ManagedChunk>();
        private readonly List<int> _neighborCountCache = new List<int>();
        private readonly List<int3> _toRemoveCache = new List<int3>();
        private readonly HashSet<int3> _chunksNeedingLightUpdate = new HashSet<int3>();
        private readonly HashSet<ManagedChunk> _generatedChunks = new HashSet<ManagedChunk>();
        private int3 _lastCameraChunkCoord = new int3(int.MinValue, int.MinValue, int.MinValue);
        private AsyncChunkSaver _asyncSaver;

        /// <summary>
        /// Schwartzian transform caches for distance-sorted unload.
        /// Resized on demand, never shrunk. Owner: ChunkManager.
        /// </summary>
        private int[] _unloadDistCache = Array.Empty<int>();
        private int3[] _unloadCoordCache = Array.Empty<int3>();

        /// <summary>
        /// Called when a block with FlagHasBlockEntity is placed.
        /// Parameters: chunkCoord, flatIndex, stateId.
        /// </summary>
        public Action<int3, int, StateId> OnBlockEntityPlaced;

        /// <summary>
        /// Called when a block with FlagHasBlockEntity is broken (replaced by air).
        /// Parameters: chunkCoord, flatIndex, oldStateId.
        /// </summary>
        public Action<int3, int, StateId> OnBlockEntityRemoved;

        /// <summary>
        /// NativeStateRegistry reference for checking HasBlockEntity flag during SetBlock.
        /// Must be set after content pipeline completes.
        /// </summary>
        private NativeStateRegistry _nativeStateRegistry;

        /// <summary>
        /// Schwartzian transform caches for forward-weighted queue sort.
        /// Resized on demand, never shrunk. Owner: ChunkManager.
        /// </summary>
        private float[] _sortScoreCache = Array.Empty<float>();
        private int3[] _sortCoordCache = Array.Empty<int3>();

        /// <summary>
        /// Cursor index into _loadQueue. Advanced instead of RemoveRange(0, count).
        /// Reset when UpdateLoadingQueue() rebuilds the queue.
        /// </summary>
        private int _loadQueueIndex;

        public int LoadedCount
        {
            get { return _chunks.Count; }
        }

        public int GeneratedChunkCount
        {
            get { return _generatedChunks.Count; }
        }

        public int PendingLoadCount
        {
            get { return _loadQueue.Count - _loadQueueIndex; }
        }

        public int RenderDistance
        {
            get { return _renderDistance; }
        }

        public void SetRenderDistance(int distance)
        {
            _renderDistance = math.max(1, distance);
        }

        /// <summary>
        /// Centralized state transition. Maintains _generatedChunks index.
        /// All external callers (schedulers) MUST use this instead of chunk.State = x.
        /// </summary>
        public void SetChunkState(ManagedChunk chunk, ChunkState newState)
        {
            ChunkState oldState = chunk.State;
            chunk.State = newState;

            if (oldState == ChunkState.Generated && newState != ChunkState.Generated)
            {
                _generatedChunks.Remove(chunk);
            }
            else if (newState == ChunkState.Generated && oldState != ChunkState.Generated)
            {
                if (!chunk.LightJobInFlight)
                {
                    _generatedChunks.Add(chunk);
                }
            }
        }

        /// <summary>
        /// Called after any change to chunk.LightJobInFlight. Keeps _generatedChunks
        /// in sync: a Generated chunk with LightJobInFlight=true is NOT mesh-eligible.
        /// </summary>
        public void NotifyLightJobChanged(ManagedChunk chunk)
        {
            if (chunk.State == ChunkState.Generated)
            {
                if (chunk.LightJobInFlight)
                {
                    _generatedChunks.Remove(chunk);
                }
                else
                {
                    _generatedChunks.Add(chunk);
                }
            }
        }

        /// <summary>
        /// Sets the NativeStateRegistry for block entity flag checks in SetBlock.
        /// Must be called after content pipeline completes and before gameplay starts.
        /// </summary>
        public void SetNativeStateRegistry(NativeStateRegistry nativeStateRegistry)
        {
            _nativeStateRegistry = nativeStateRegistry;
        }

        /// <summary>
        /// Sets the async chunk saver for off-thread serialization during unload.
        /// Must be called after construction and before gameplay starts.
        /// </summary>
        public void SetAsyncSaver(AsyncChunkSaver asyncSaver)
        {
            _asyncSaver = asyncSaver;
        }

        public ChunkManager(
            ChunkPool pool,
            int renderDistance,
            int yLoadMin = -1,
            int yLoadMax = 3,
            int yUnloadMin = -2,
            int yUnloadMax = 4)
        {
            _pool = pool;
            _renderDistance = renderDistance;
            _yLoadMin = yLoadMin;
            _yLoadMax = yLoadMax;
            _yUnloadMin = yUnloadMin;
            _yUnloadMax = yUnloadMax;
        }

        public void UpdateLoadingQueue(int3 cameraChunkCoord, float3 cameraForward)
        {
            int3 diff = cameraChunkCoord - _lastCameraChunkCoord;
            int movedDist = math.max(math.abs(diff.x), math.max(math.abs(diff.y), math.abs(diff.z)));

            // Only rebuild if camera moved far enough OR the queue is empty.
            // Prevents constant queue thrashing in fly mode.
            bool queueHasWork = _loadQueue.Count - _loadQueueIndex > 0;
            if (queueHasWork && movedDist < 2)
            {
                return;
            }

            _lastCameraChunkCoord = cameraChunkCoord;
            _loadQueue.Clear();
            _loadQueueIndex = 0;

            // Spiral order from center
            for (int d = 0; d <= _renderDistance; d++)
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
                            int3 coord = cameraChunkCoord + new int3(x, y, z);

                            if (!_chunks.ContainsKey(coord))
                            {
                                _loadQueue.Add(coord);
                            }
                        }
                    }
                }
            }

            // Schwartzian transform: pre-compute sort scores to avoid
            // repeated normalizesafe (rsqrt) inside O(n log n) comparisons.
            // score = dist² * (2 - dot), so forward chunks score lower (higher priority).
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

                int3 d = coord - cameraChunkCoord;
                float3 dirXZ = math.normalizesafe(new float3(d.x, 0, d.z));
                float dot = math.dot(dirXZ, forwardXZ);
                float distSq = math.lengthsq(d);
                _sortScoreCache[i] = distSq * (2.0f - dot);
            }

            Array.Sort(_sortScoreCache, _sortCoordCache, 0, count);

            _loadQueue.Clear();

            for (int i = 0; i < count; i++)
            {
                _loadQueue.Add(_sortCoordCache[i]);
            }
        }

        public void FillChunksToGenerate(List<ManagedChunk> result, int maxCount)
        {
            result.Clear();
            int created = 0;

            while (_loadQueueIndex < _loadQueue.Count && created < maxCount)
            {
                int3 coord = _loadQueue[_loadQueueIndex];
                _loadQueueIndex++;

                // Skip already-loaded chunks without consuming budget
                if (_chunks.ContainsKey(coord))
                {
                    continue;
                }

                NativeArray<StateId> data = _pool.Checkout();
                ManagedChunk chunk = new ManagedChunk(coord, data);
                chunk.State = ChunkState.Generating;
                _chunks[coord] = chunk;
                result.Add(chunk);
                created++;
            }

            // If cursor has consumed the entire queue, clear to free memory
            if (_loadQueueIndex >= _loadQueue.Count)
            {
                _loadQueue.Clear();
                _loadQueueIndex = 0;
            }
        }

        /// <summary>
        /// Fills the provided list with chunks in the Generated state, sorted by
        /// ready neighbor count (descending). Uses cached lists to avoid per-frame
        /// allocations. The caller must NOT store the result list reference beyond
        /// the current frame.
        /// </summary>
        public void FillChunksToMesh(List<ManagedChunk> result, int maxCount)
        {
            result.Clear();
            _meshCandidateCache.Clear();
            _neighborCountCache.Clear();

            // --- Single-pass top-K selection over _generatedChunks ---
            // Buffer stays at most maxCount elements. For each incoming chunk,
            // we compare against the worst element in the buffer.

            foreach (ManagedChunk chunk in _generatedChunks)
            {
                int nc = CountReadyNeighbors(chunk.Coord);
                bool neverMeshed = chunk.RenderedLODLevel < 0;

                if (_meshCandidateCache.Count < maxCount)
                {
                    // Buffer not full — insert unconditionally
                    _meshCandidateCache.Add(chunk);
                    _neighborCountCache.Add(nc);
                }
                else
                {
                    // Find worst element in buffer
                    int worstIdx = 0;

                    for (int w = 1; w < _meshCandidateCache.Count; w++)
                    {
                        if (IsWorseThan(w, worstIdx))
                        {
                            worstIdx = w;
                        }
                    }

                    // Compare incoming chunk against worst
                    bool worstNeverMeshed = _meshCandidateCache[worstIdx].RenderedLODLevel < 0;

                    if (IsBetterPriority(neverMeshed, nc, worstNeverMeshed, _neighborCountCache[worstIdx]))
                    {
                        _meshCandidateCache[worstIdx] = chunk;
                        _neighborCountCache[worstIdx] = nc;
                    }
                }
            }

            // Sort the small buffer (maxCount elements max — insertion sort is fine here)
            for (int i = 1; i < _meshCandidateCache.Count; i++)
            {
                ManagedChunk tempChunk = _meshCandidateCache[i];
                int tempCount = _neighborCountCache[i];
                bool tempNeverMeshed = tempChunk.RenderedLODLevel < 0;
                int j = i - 1;

                while (j >= 0)
                {
                    bool jNeverMeshed = _meshCandidateCache[j].RenderedLODLevel < 0;

                    if (tempNeverMeshed && !jNeverMeshed)
                    {
                        break;
                    }

                    bool sameGroup = tempNeverMeshed == jNeverMeshed;

                    if (sameGroup && _neighborCountCache[j] >= tempCount)
                    {
                        break;
                    }

                    if (!sameGroup)
                    {
                        _meshCandidateCache[j + 1] = _meshCandidateCache[j];
                        _neighborCountCache[j + 1] = _neighborCountCache[j];
                        j--;
                        continue;
                    }

                    _meshCandidateCache[j + 1] = _meshCandidateCache[j];
                    _neighborCountCache[j + 1] = _neighborCountCache[j];
                    j--;
                }

                _meshCandidateCache[j + 1] = tempChunk;
                _neighborCountCache[j + 1] = tempCount;
            }

            for (int i = 0; i < _meshCandidateCache.Count; i++)
            {
                result.Add(_meshCandidateCache[i]);
            }
        }

        /// <summary>
        /// Returns true if buffer element at indexA is worse priority than element at indexB.
        /// Worse = already-meshed when other is never-meshed, or fewer neighbors in same group.
        /// </summary>
        private bool IsWorseThan(int indexA, int indexB)
        {
            bool aNeverMeshed = _meshCandidateCache[indexA].RenderedLODLevel < 0;
            bool bNeverMeshed = _meshCandidateCache[indexB].RenderedLODLevel < 0;

            if (aNeverMeshed != bNeverMeshed)
            {
                return !aNeverMeshed;
            }

            return _neighborCountCache[indexA] < _neighborCountCache[indexB];
        }

        /// <summary>
        /// Returns true if (neverMeshed, neighborCount) is strictly better priority
        /// than (worstNeverMeshed, worstNc).
        /// </summary>
        private static bool IsBetterPriority(
            bool neverMeshed, int neighborCount,
            bool worstNeverMeshed, int worstNc)
        {
            if (neverMeshed != worstNeverMeshed)
            {
                return neverMeshed;
            }

            return neighborCount > worstNc;
        }

        /// <summary>
        /// Backward-compatible wrapper. Callers that cannot be updated yet
        /// may use this, but prefer FillChunksToMesh for zero-allocation usage.
        /// </summary>
        public List<ManagedChunk> GetChunksToMesh(int maxCount)
        {
            List<ManagedChunk> result = new List<ManagedChunk>();
            FillChunksToMesh(result, maxCount);

            return result;
        }

        private int CountReadyNeighbors(int3 coord)
        {
            int count = 0;

            if (HasGeneratedNeighbor(coord + new int3(1, 0, 0))) { count++; }
            if (HasGeneratedNeighbor(coord + new int3(-1, 0, 0))) { count++; }
            if (HasGeneratedNeighbor(coord + new int3(0, 1, 0))) { count++; }
            if (HasGeneratedNeighbor(coord + new int3(0, -1, 0))) { count++; }
            if (HasGeneratedNeighbor(coord + new int3(0, 0, 1))) { count++; }
            if (HasGeneratedNeighbor(coord + new int3(0, 0, -1))) { count++; }

            return count;
        }

        private bool HasGeneratedNeighbor(int3 coord)
        {
            return _chunks.TryGetValue(coord, out ManagedChunk neighbor)
                && neighbor.State >= ChunkState.RelightPending;
        }

        /// <summary>
        /// Fills with all chunks in Generated state.
        /// Used by LODScheduler to assign LOD levels before meshing.
        /// </summary>
        public void FillGeneratedChunks(List<ManagedChunk> result)
        {
            result.Clear();

            foreach (ManagedChunk chunk in _generatedChunks)
            {
                result.Add(chunk);
            }
        }

        /// <summary>
        /// Fills with Generated chunks that have LODLevel > 0.
        /// These need LOD meshing, not full-detail meshing.
        /// </summary>
        public void FillGeneratedChunksWithLOD(List<ManagedChunk> result)
        {
            result.Clear();

            foreach (ManagedChunk chunk in _generatedChunks)
            {
                if (chunk.LODLevel > 0)
                {
                    result.Add(chunk);
                }
            }
        }

        /// <summary>
        /// Fills the provided list with all chunks in the Ready state.
        /// Clears the list before filling. Used for LOD level assignment.
        /// </summary>
        public void FillReadyChunks(List<ManagedChunk> result)
        {
            result.Clear();

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                if (kvp.Value.State == ChunkState.Ready)
                {
                    result.Add(kvp.Value);
                }
            }
        }

        /// <summary>
        /// Fills the provided list with all chunks that need cross-chunk light updates.
        /// Iterates only the dirty set instead of all loaded chunks — O(dirty) not O(all).
        /// Clears the list before filling.
        /// </summary>
        public void FillChunksNeedingLightUpdate(List<ManagedChunk> result)
        {
            result.Clear();

            foreach (int3 coord in _chunksNeedingLightUpdate)
            {
                if (_chunks.TryGetValue(coord, out ManagedChunk chunk) &&
                    chunk.NeedsLightUpdate && !chunk.LightJobInFlight)
                {
                    result.Add(chunk);
                }
            }
        }

        /// <summary>
        /// Marks a chunk as needing a cross-chunk light update and adds it to the dirty set.
        /// Call this instead of setting chunk.NeedsLightUpdate = true directly.
        /// </summary>
        public void MarkNeedsLightUpdate(int3 coord)
        {
            if (_chunks.TryGetValue(coord, out ManagedChunk chunk))
            {
                chunk.NeedsLightUpdate = true;
                _chunksNeedingLightUpdate.Add(coord);
            }
        }

        /// <summary>
        /// Clears the NeedsLightUpdate flag on a chunk and removes it from the dirty set.
        /// Call this instead of setting chunk.NeedsLightUpdate = false directly.
        /// </summary>
        public void ClearNeedsLightUpdate(int3 coord)
        {
            if (_chunks.TryGetValue(coord, out ManagedChunk chunk))
            {
                chunk.NeedsLightUpdate = false;
            }

            _chunksNeedingLightUpdate.Remove(coord);
        }

        /// <summary>
        /// Fills the provided list with all chunks in the RelightPending state.
        /// Clears the list before filling.
        /// </summary>
        public void FillChunksNeedingRelight(List<ManagedChunk> result)
        {
            result.Clear();

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                if (kvp.Value.State == ChunkState.RelightPending && !kvp.Value.LightJobInFlight)
                {
                    result.Add(kvp.Value);
                }
            }
        }

        public ManagedChunk GetChunk(int3 coord)
        {
            _chunks.TryGetValue(coord, out ManagedChunk chunk);

            return chunk;
        }

        /// <summary>
        /// Returns true if the block at the given world coordinate is in a loaded,
        /// sufficiently-generated chunk. Returns false if the chunk is missing,
        /// still generating, or has no data.
        /// </summary>
        public bool IsBlockLoaded(int3 worldCoord)
        {
            int3 chunkCoord = WorldToChunk(worldCoord);
            ManagedChunk chunk = GetChunk(chunkCoord);

            return chunk != null && chunk.State >= ChunkState.RelightPending && chunk.Data.IsCreated;
        }

        /// <summary>
        /// Gets the StateId at a world-space block coordinate.
        /// Returns StateId.Air if the chunk is not loaded or not yet generated.
        /// </summary>
        public StateId GetBlock(int3 worldCoord)
        {
            int3 chunkCoord = WorldToChunk(worldCoord);
            ManagedChunk chunk = GetChunk(chunkCoord);

            if (chunk == null || chunk.State < ChunkState.RelightPending || !chunk.Data.IsCreated)
            {
                return StateId.Air;
            }

            int localX = worldCoord.x - chunkCoord.x * ChunkConstants.Size;
            int localY = worldCoord.y - chunkCoord.y * ChunkConstants.Size;
            int localZ = worldCoord.z - chunkCoord.z * ChunkConstants.Size;
            int index = ChunkData.GetIndex(localX, localY, localZ);

            NativeArray<StateId> data = chunk.Data;

            return data[index];
        }

        /// <summary>
        /// Sets the StateId at a world-space block coordinate.
        /// Marks the chunk (and border-adjacent neighbors) for remeshing.
        /// Sets the chunk to RelightPending so light is recalculated before remesh.
        /// Does nothing if the chunk is not loaded or still generating.
        /// </summary>
        public void SetBlock(int3 worldCoord, StateId state, List<int3> dirtiedChunks)
        {
            int3 chunkCoord = WorldToChunk(worldCoord);
            ManagedChunk chunk = GetChunk(chunkCoord);

            if (chunk == null || chunk.State < ChunkState.RelightPending || !chunk.Data.IsCreated)
            {
                return;
            }

            int localX = worldCoord.x - chunkCoord.x * ChunkConstants.Size;
            int localY = worldCoord.y - chunkCoord.y * ChunkConstants.Size;
            int localZ = worldCoord.z - chunkCoord.z * ChunkConstants.Size;
            int index = ChunkData.GetIndex(localX, localY, localZ);

            chunk.IsDirty = true;

            // Check block entity flags for old and new states
            StateId oldState = chunk.Data[index];
            bool oldHasEntity = _nativeStateRegistry.States.IsCreated &&
                oldState.Value < _nativeStateRegistry.States.Length &&
                _nativeStateRegistry.States[oldState.Value].HasBlockEntity;
            bool newHasEntity = _nativeStateRegistry.States.IsCreated &&
                state.Value < _nativeStateRegistry.States.Length &&
                _nativeStateRegistry.States[state.Value].HasBlockEntity;

            if (chunk.State == ChunkState.Meshing)
            {
                // Defer the edit — do NOT write to ChunkData while the mesh job is
                // reading it. The edit will be applied in ApplyDeferredEdits() after
                // the job finishes. Block entity events are also deferred.
                chunk.DeferredEdits.Add(new DeferredEdit
                {
                    FlatIndex = index,
                    OldState = oldState,
                    NewState = state,
                });
            }
            else
            {
                // Normal path: write immediately and trigger relight
                NativeArray<StateId> data = chunk.Data;
                data[index] = state;
                chunk.PendingEditIndices.Add(index);
                SetChunkState(chunk, ChunkState.RelightPending);

                // Fire block entity events only on the immediate path
                if (oldHasEntity && !newHasEntity)
                {
                    OnBlockEntityRemoved?.Invoke(chunkCoord, index, oldState);
                }

                if (newHasEntity && !oldHasEntity)
                {
                    OnBlockEntityPlaced?.Invoke(chunkCoord, index, state);
                }
            }

            dirtiedChunks.Add(chunkCoord);
            DirtyNeighborBorders(chunkCoord, localX, localY, localZ, dirtiedChunks);
        }

        /// <summary>
        /// Applies deferred edits that arrived while a chunk was in Meshing state.
        /// Writes the edits to ChunkData and fires block entity events for each edit.
        /// Called by MeshScheduler.PollCompleted after the mesh job finishes.
        /// </summary>
        public void ApplyDeferredEdits(ManagedChunk chunk)
        {
            NativeArray<StateId> chunkData = chunk.Data;

            for (int di = 0; di < chunk.DeferredEdits.Count; di++)
            {
                DeferredEdit edit = chunk.DeferredEdits[di];
                chunkData[edit.FlatIndex] = edit.NewState;
                chunk.PendingEditIndices.Add(edit.FlatIndex);

                // Fire block entity events now that the voxel write has happened
                bool editOldHasEntity = _nativeStateRegistry.States.IsCreated &&
                    edit.OldState.Value < _nativeStateRegistry.States.Length &&
                    _nativeStateRegistry.States[edit.OldState.Value].HasBlockEntity;
                bool editNewHasEntity = _nativeStateRegistry.States.IsCreated &&
                    edit.NewState.Value < _nativeStateRegistry.States.Length &&
                    _nativeStateRegistry.States[edit.NewState.Value].HasBlockEntity;

                if (editOldHasEntity && !editNewHasEntity)
                {
                    OnBlockEntityRemoved?.Invoke(chunk.Coord, edit.FlatIndex, edit.OldState);
                }

                if (editNewHasEntity && !editOldHasEntity)
                {
                    OnBlockEntityPlaced?.Invoke(chunk.Coord, edit.FlatIndex, edit.NewState);
                }
            }

            chunk.DeferredEdits.Clear();
            SetChunkState(chunk, ChunkState.RelightPending);
        }

        /// <summary>
        /// Dirties neighbor chunks when an edit is on a chunk border face (local coord 0 or 31).
        /// Extracted to avoid duplication between normal and deferred edit paths.
        /// </summary>
        private void DirtyNeighborBorders(int3 chunkCoord, int localX, int localY, int localZ,
            List<int3> dirtiedChunks)
        {
            if (localX == 0)
            {
                DirtyNeighborChunk(chunkCoord + new int3(-1, 0, 0), dirtiedChunks);
            }

            if (localX == ChunkConstants.SizeMask)
            {
                DirtyNeighborChunk(chunkCoord + new int3(1, 0, 0), dirtiedChunks);
            }

            if (localY == 0)
            {
                DirtyNeighborChunk(chunkCoord + new int3(0, -1, 0), dirtiedChunks);
            }

            if (localY == ChunkConstants.SizeMask)
            {
                DirtyNeighborChunk(chunkCoord + new int3(0, 1, 0), dirtiedChunks);
            }

            if (localZ == 0)
            {
                DirtyNeighborChunk(chunkCoord + new int3(0, 0, -1), dirtiedChunks);
            }

            if (localZ == ChunkConstants.SizeMask)
            {
                DirtyNeighborChunk(chunkCoord + new int3(0, 0, 1), dirtiedChunks);
            }
        }

        private void DirtyNeighborChunk(int3 neighborCoord, List<int3> dirtiedChunks)
        {
            ManagedChunk neighbor = GetChunk(neighborCoord);

            if (neighbor == null || neighbor.State < ChunkState.RelightPending)
            {
                return;
            }

            if (neighbor.State == ChunkState.Ready)
            {
                SetChunkState(neighbor, ChunkState.Generated);
                dirtiedChunks.Add(neighborCoord);
            }
            else if (neighbor.State == ChunkState.Meshing)
            {
                neighbor.NeedsRemesh = true;
            }
        }

        /// <summary>
        /// Converts a world-space block coordinate to a chunk coordinate.
        /// Uses floor division to handle negative coordinates correctly.
        /// </summary>
        public static int3 WorldToChunk(int3 worldCoord)
        {
            return new int3(
                FloorDiv(worldCoord.x, ChunkConstants.Size),
                FloorDiv(worldCoord.y, ChunkConstants.Size),
                FloorDiv(worldCoord.z, ChunkConstants.Size));
        }

        private static int FloorDiv(int a, int b)
        {
            return a >= 0 ? a / b : (a - b + 1) / b;
        }

        public void UnloadDistantChunks(
            int3 cameraChunkCoord,
            List<int3> unloaded,
            WorldStorage worldStorage = null,
            float budgetMs = 2.0f)
        {
            unloaded.Clear();
            _toRemoveCache.Clear();

            // Phase 1: Collect all out-of-range candidates (distance test only)
            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                int3 diff = kvp.Key - cameraChunkCoord;
                int xzDist = math.max(math.abs(diff.x), math.abs(diff.z));
                bool yOutOfRange = diff.y < _yUnloadMin || diff.y > _yUnloadMax;

                if (xzDist > _renderDistance + 1 || yOutOfRange)
                {
                    _toRemoveCache.Add(kvp.Key);
                }
            }

            int candidateCount = _toRemoveCache.Count;

            if (candidateCount == 0)
            {
                return;
            }

            // Phase 2: Schwartzian sort by Chebyshev XZ distance (ascending)
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
                int3 diff = coord - cameraChunkCoord;
                _unloadDistCache[i] = math.max(math.abs(diff.x), math.abs(diff.z));
            }

            Array.Sort(_unloadDistCache, _unloadCoordCache, 0, candidateCount);

            // Phase 3: Process within budget, farthest first (iterate in reverse)
            long startTicks = Stopwatch.GetTimestamp();
            double ticksPerMs = Stopwatch.Frequency / 1000.0;

            for (int i = candidateCount - 1; i >= 0; i--)
            {
                int3 coord = _unloadCoordCache[i];
                ManagedChunk chunk = _chunks[coord];

                chunk.ActiveJobHandle.Complete();

                // Notify block entities of unload
                if (chunk.BlockEntities != null)
                {
                    foreach (KeyValuePair<int, IBlockEntity> bePair in chunk.BlockEntities)
                    {
                        bePair.Value.OnChunkUnload();
                    }
                }

                // Save modified chunks before unloading (enqueue must precede dispose)
                if (chunk.IsDirty && chunk.Data.IsCreated)
                {
                    if (_asyncSaver != null)
                    {
                        _asyncSaver.EnqueueSave(
                            chunk.Coord, chunk.Data, chunk.LightData, chunk.BlockEntities);
                    }
                    else if (worldStorage != null)
                    {
                        worldStorage.SaveChunk(
                            chunk.Coord, chunk.Data, chunk.LightData, chunk.BlockEntities);
                    }

                    chunk.IsDirty = false;
                }

                if (chunk.Data.IsCreated)
                {
                    _pool.Return(chunk.Data);
                }

                if (chunk.LightData.IsCreated)
                {
                    chunk.LightData.Dispose();
                }

                if (chunk.HeightMap.IsCreated)
                {
                    chunk.HeightMap.Dispose();
                }

                if (chunk.RiverFlags.IsCreated)
                {
                    chunk.RiverFlags.Dispose();
                }

                unloaded.Add(coord);

                // Check budget
                double elapsedMs = (Stopwatch.GetTimestamp() - startTicks) / ticksPerMs;

                if (elapsedMs >= budgetMs)
                {
                    break;
                }
            }

            // Phase 4: Remove processed chunks from _chunks and index sets
            for (int i = 0; i < unloaded.Count; i++)
            {
                if (_chunks.TryGetValue(unloaded[i], out ManagedChunk unloadedChunk))
                {
                    _generatedChunks.Remove(unloadedChunk);
                }

                _chunksNeedingLightUpdate.Remove(unloaded[i]);
                _chunks.Remove(unloaded[i]);
            }
        }

        public void SaveAllChunks(WorldStorage storage)
        {
            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                ManagedChunk chunk = kvp.Value;

                if (chunk.IsDirty && chunk.State >= ChunkState.RelightPending && chunk.Data.IsCreated)
                {
                    chunk.ActiveJobHandle.Complete();
                    storage.SaveChunk(chunk.Coord, chunk.Data, chunk.LightData, chunk.BlockEntities);
                    chunk.IsDirty = false;
                }
            }
        }

        private static readonly int3[] s_neighborOffsets = new int3[]
        {
            new int3(1, 0, 0),
            new int3(-1, 0, 0),
            new int3(0, 1, 0),
            new int3(0, -1, 0),
            new int3(0, 0, 1),
            new int3(0, 0, -1),
        };

        /// <summary>
        /// Marks all Ready neighbors of the given coord as Generated (needing remesh),
        /// and flags any Meshing neighbors for re-mesh after their current job completes.
        /// Called after a chunk finishes generation or is loaded from storage.
        /// </summary>
        /// <summary>
        /// Fills the given array with chunk counts per state. The array must have
        /// length >= ChunkState enum count (8). Also outputs NeedsRemesh and
        /// NeedsLightUpdate totals. Single iteration over _chunks.
        /// </summary>
        public void FillStateHistogram(int[] countsByState, out int needsRemesh, out int needsLightUpdate)
        {
            for (int i = 0; i < countsByState.Length; i++)
            {
                countsByState[i] = 0;
            }

            needsRemesh = 0;
            needsLightUpdate = 0;

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                ManagedChunk chunk = kvp.Value;
                int stateIdx = (int)chunk.State;

                if (stateIdx >= 0 && stateIdx < countsByState.Length)
                {
                    countsByState[stateIdx]++;
                }

                if (chunk.NeedsRemesh)
                {
                    needsRemesh++;
                }

                if (chunk.NeedsLightUpdate)
                {
                    needsLightUpdate++;
                }
            }
        }

        public void InvalidateReadyNeighbors(int3 coord)
        {
            for (int i = 0; i < s_neighborOffsets.Length; i++)
            {
                int3 neighborCoord = coord + s_neighborOffsets[i];
                ManagedChunk neighbor = GetChunk(neighborCoord);

                if (neighbor == null)
                {
                    continue;
                }

                if (neighbor.State == ChunkState.Ready)
                {
                    SetChunkState(neighbor, ChunkState.Generated);
                }
                else if (neighbor.State == ChunkState.Meshing)
                {
                    // Job is active — flag for re-mesh after it completes
                    neighbor.NeedsRemesh = true;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                kvp.Value.ActiveJobHandle.Complete();

                if (kvp.Value.Data.IsCreated)
                {
                    _pool.Return(kvp.Value.Data);
                }

                if (kvp.Value.LightData.IsCreated)
                {
                    kvp.Value.LightData.Dispose();
                }

                if (kvp.Value.HeightMap.IsCreated)
                {
                    kvp.Value.HeightMap.Dispose();
                }

                if (kvp.Value.RiverFlags.IsCreated)
                {
                    kvp.Value.RiverFlags.Dispose();
                }
            }

            _chunks.Clear();
            _chunksNeedingLightUpdate.Clear();
            _generatedChunks.Clear();
        }
    }
}
