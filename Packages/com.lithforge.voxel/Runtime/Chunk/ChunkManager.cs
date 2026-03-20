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
    /// <summary>
    ///     Central authority for chunk lifecycle: loading, unloading, state transitions,
    ///     block edits, and query methods consumed by schedulers. Owns the loaded chunk
    ///     dictionary and secondary indices. Delegates to internal helpers:
    ///     <see cref="ChunkEditor" />, <see cref="ChunkLoadQueue" />, <see cref="ChunkStateIndex" />.
    ///     <remarks>
    ///         All ChunkState transitions must go through <see cref="SetChunkState" /> so that
    ///         secondary indices stay consistent. Direct assignment to chunk.State is forbidden
    ///         outside this class.
    ///     </remarks>
    /// </summary>
    public sealed class ChunkManager : IDisposable
    {
        /// <summary>
        ///     Face-indexed neighbor offsets: 0=+X, 1=-X, 2=+Y, 3=-Y, 4=+Z, 5=-Z.
        /// </summary>
        public static readonly int3[] FaceOffsets =
        {
            new(1, 0, 0),  // 0: +X
            new(-1, 0, 0), // 1: -X
            new(0, 1, 0),  // 2: +Y
            new(0, -1, 0), // 3: -Y
            new(0, 0, 1),  // 4: +Z
            new(0, 0, -1), // 5: -Z
        };

        /// <summary>Alias for FaceOffsets used internally.</summary>
        private static readonly int3[] s_neighborOffsets = FaceOffsets;

        /// <summary>
        ///     Maps face index to its opposite: +X(0)↔-X(1), +Y(2)↔-Y(3), +Z(4)↔-Z(5).
        ///     Used by RegisterChunk, UnregisterChunk for bitmask updates.
        /// </summary>
        private static readonly int[] s_oppositeFace =
        {
            1,
            0,
            3,
            2,
            5,
            4,
        };

        /// <summary>
        ///     Primary dictionary of all loaded chunks, keyed by chunk coordinate.
        ///     Copy-on-write: the main thread replaces the entire reference on mutation;
        ///     the server background thread reads a consistent snapshot via volatile read.
        /// </summary>
        private volatile Dictionary<int3, ManagedChunk> _chunks = new();

        /// <summary>ChunkPool for NativeArray checkout and return.</summary>
        private readonly ChunkPool _pool;

        /// <summary>Optional background chunk saver for async serialization on unload.</summary>
        private AsyncChunkSaver _asyncSaver;

        /// <summary>Whether this ChunkManager has been disposed.</summary>
        private bool _disposed;

        /// <summary>Minimum chunk Y coordinate (inclusive) for loading.</summary>
        private readonly int _yLoadMin;

        /// <summary>Maximum chunk Y coordinate (inclusive) for loading.</summary>
        private readonly int _yLoadMax;

        /// <summary>Internal helper for block editing operations.</summary>
        private readonly ChunkEditor _editor;

        /// <summary>Internal helper for load queue management.</summary>
        private readonly ChunkLoadQueue _loadQueue;

        /// <summary>Internal helper for state-based secondary indices.</summary>
        private readonly ChunkStateIndex _stateIndex;

        /// <summary>Reusable list for collecting sorted unload candidates from ChunkLoadQueue.</summary>
        private readonly List<int3> _unloadCandidateCache = new();

        /// <summary>Reusable list for collecting coordinates to generate from the load queue.</summary>
        private readonly List<int3> _coordsToGenerateCache = new();

        /// <summary>
        ///     Called after any block change (both immediate and deferred paths).
        ///     Parameters: worldCoord, newStateId.
        ///     Used by <see cref="Network.ChunkDirtyTracker" /> for network delta sync.
        /// </summary>
        public Action<int3, StateId> OnBlockChanged
        {
            get { return _editor.OnBlockChanged; }
            set { _editor.OnBlockChanged = value; }
        }

        /// <summary>
        ///     Called when a block with FlagHasBlockEntity is placed.
        ///     Parameters: chunkCoord, flatIndex, stateId.
        /// </summary>
        public Action<int3, int, StateId> OnBlockEntityPlaced
        {
            get { return _editor.OnBlockEntityPlaced; }
            set { _editor.OnBlockEntityPlaced = value; }
        }

        /// <summary>
        ///     Called when a block with FlagHasBlockEntity is broken (replaced by air).
        ///     Parameters: chunkCoord, flatIndex, oldStateId.
        /// </summary>
        public Action<int3, int, StateId> OnBlockEntityRemoved
        {
            get { return _editor.OnBlockEntityRemoved; }
            set { _editor.OnBlockEntityRemoved = value; }
        }

        /// <summary>Creates a ChunkManager with the given pool, render distance, and Y load/unload bounds.</summary>
        public ChunkManager(
            ChunkPool pool,
            int renderDistance,
            int yLoadMin = -1,
            int yLoadMax = 3,
            int yUnloadMin = -2,
            int yUnloadMax = 4)
        {
            _pool = pool;
            _yLoadMin = yLoadMin;
            _yLoadMax = yLoadMax;

            _editor = new ChunkEditor(GetChunk, SetChunkState);
            _loadQueue = new ChunkLoadQueue(renderDistance, yLoadMin, yLoadMax, yUnloadMin, yUnloadMax);
            _stateIndex = new ChunkStateIndex();
        }

        /// <summary>Adds a chunk via copy-on-write. Main thread only.</summary>
        private void AddChunk(int3 coord, ManagedChunk chunk)
        {
            Dictionary<int3, ManagedChunk> copy = new(_chunks);
            copy[coord] = chunk;
            _chunks = copy;
        }

        /// <summary>Removes a chunk via copy-on-write. Main thread only.</summary>
        private bool RemoveChunk(int3 coord, out ManagedChunk removed)
        {
            Dictionary<int3, ManagedChunk> snapshot = _chunks;

            if (!snapshot.TryGetValue(coord, out removed))
            {
                return false;
            }

            Dictionary<int3, ManagedChunk> copy = new(snapshot);
            copy.Remove(coord);
            _chunks = copy;

            return true;
        }

        /// <summary>Total number of currently loaded chunks.</summary>
        public int LoadedCount
        {
            get { return _chunks.Count; }
        }

        /// <summary>Number of chunks in the Generated state (mesh-eligible).</summary>
        public int GeneratedChunkCount
        {
            get { return _stateIndex.GeneratedChunkCount; }
        }

        /// <summary>Number of remaining entries in the load queue.</summary>
        public int PendingLoadCount
        {
            get { return _loadQueue.PendingLoadCount; }
        }

        /// <summary>Current render distance in chunks (Chebyshev XZ radius).</summary>
        public int RenderDistance
        {
            get { return _loadQueue.RenderDistance; }
        }

        /// <summary>Completes all in-flight jobs and returns all chunk NativeArrays to the pool.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                ManagedChunk chunk = kvp.Value;

                chunk.ActiveJobHandle.Complete();

                if (chunk.Data.IsCreated)
                {
                    _pool.Return(chunk.Data);

                    chunk.Data = default;
                }

                if (chunk.LightData.IsCreated)
                {
                    NativeArray<byte> ld = chunk.LightData;

                    ld.Dispose();

                    chunk.LightData = default;
                }

                if (chunk.HeightMap.IsCreated)
                {
                    NativeArray<int> hm = chunk.HeightMap;

                    hm.Dispose();

                    chunk.HeightMap = default;
                }

                if (chunk.RiverFlags.IsCreated)
                {
                    NativeArray<byte> rf = chunk.RiverFlags;

                    rf.Dispose();

                    chunk.RiverFlags = default;
                }
            }

            _chunks.Clear();
            _stateIndex.Clear();
        }

        /// <summary>Updates the render distance (clamped to at least 1).</summary>
        public void SetRenderDistance(int distance)
        {
            _loadQueue.SetRenderDistance(distance);
        }

        /// <summary>
        ///     Centralized state transition. Maintains all secondary indices
        ///     (_generatedChunks, _readyChunks, _relightPendingChunks).
        ///     All external callers (schedulers) MUST use this instead of chunk.State = x.
        /// </summary>
        public void SetChunkState(ManagedChunk chunk, ChunkState newState)
        {
            _stateIndex.SetChunkState(chunk, newState);
        }

        /// <summary>
        ///     Called after any change to chunk.LightJobInFlight. Keeps _generatedChunks
        ///     in sync: a Generated chunk with LightJobInFlight=true is NOT mesh-eligible.
        /// </summary>
        public void NotifyLightJobChanged(ManagedChunk chunk)
        {
            _stateIndex.NotifyLightJobChanged(chunk);
        }

        /// <summary>
        ///     Sets the NativeStateRegistry for block entity flag checks in SetBlock.
        ///     Must be called after content pipeline completes and before gameplay starts.
        /// </summary>
        public void SetNativeStateRegistry(NativeStateRegistry nativeStateRegistry)
        {
            _editor.SetNativeStateRegistry(nativeStateRegistry);
        }

        /// <summary>
        ///     Sets the async chunk saver for off-thread serialization during unload.
        ///     Must be called after construction and before gameplay starts.
        /// </summary>
        public void SetAsyncSaver(AsyncChunkSaver asyncSaver)
        {
            _asyncSaver = asyncSaver;
        }

        /// <summary>
        ///     Rebuilds the loading queue as the union of all player interest regions,
        ///     sorted by minimum forward-weighted distance to any player origin.
        ///     Only rebuilds if any origin moved far enough or the queue is empty.
        /// </summary>
        public void UpdateLoadingQueue(ReadOnlySpan<int3> playerChunkCoords, float3 cameraForward)
        {
            _loadQueue.UpdateLoadingQueue(playerChunkCoords, cameraForward, _chunks);
        }

        /// <summary>
        ///     Single-player backward-compatible wrapper for <see cref="UpdateLoadingQueue(ReadOnlySpan{int3}, float3)" />.
        /// </summary>
        public void UpdateLoadingQueue(int3 cameraChunkCoord, float3 cameraForward)
        {
            _loadQueue.UpdateLoadingQueue(cameraChunkCoord, cameraForward, _chunks);
        }

        /// <summary>
        ///     Recomputes per-chunk reference counts from the current set of player chunk origins.
        ///     For each loaded chunk: counts how many players have it within their interest sphere.
        ///     When RefCount drops from positive to zero, sets the grace period expiry timestamp.
        ///     Called after UpdateLoadingQueue with the same player coordinates.
        /// </summary>
        public void AdjustRefCounts(
            ReadOnlySpan<int3> playerChunkCoords,
            double currentRealtime,
            double gracePeriodSeconds)
        {
            _loadQueue.AdjustRefCounts(playerChunkCoords, currentRealtime, gracePeriodSeconds, _chunks);
        }

        /// <summary>
        ///     Pops up to maxCount entries from the load queue, checks out NativeArrays
        ///     from the pool, creates ManagedChunks, and returns them for generation scheduling.
        /// </summary>
        public void FillChunksToGenerate(List<ManagedChunk> result, int maxCount)
        {
            _loadQueue.FillCoordsToGenerate(_coordsToGenerateCache, maxCount, _chunks);

            result.Clear();

            for (int i = 0; i < _coordsToGenerateCache.Count; i++)
            {
                int3 coord = _coordsToGenerateCache[i];
                NativeArray<StateId> data = _pool.Checkout();
                ManagedChunk chunk = new(coord, data);
                AddChunk(coord, chunk);
                RegisterChunk(chunk);
                SetChunkState(chunk, ChunkState.Generating);
                result.Add(chunk);
            }
        }

        /// <summary>
        ///     Fills the provided list with Generated chunks, prioritized for meshing.
        ///     Priority: hasPlayerEdit (DESC) > neverMeshed (DESC) > hasAllNeighbors (DESC) > distScore (ASC).
        /// </summary>
        public void FillChunksToMesh(
            List<ManagedChunk> result,
            int maxCount,
            int3 cameraChunkCoord,
            float3 cameraForwardXZ)
        {
            _stateIndex.FillChunksToMesh(
                result, maxCount, cameraChunkCoord, cameraForwardXZ,
                _loadQueue.LastPlayerChunkCoords);
        }

        /// <summary>
        ///     Fills with all chunks in Generated state.
        ///     Used by LODScheduler to assign LOD levels before meshing.
        /// </summary>
        public void FillGeneratedChunks(List<ManagedChunk> result)
        {
            _stateIndex.FillGeneratedChunks(result);
        }

        /// <summary>
        ///     Fills with Generated chunks that have LODLevel > 0.
        ///     These need LOD meshing, not full-detail meshing.
        /// </summary>
        public void FillGeneratedChunksWithLOD(List<ManagedChunk> result)
        {
            _stateIndex.FillGeneratedChunksWithLOD(result);
        }

        /// <summary>
        ///     Fills the provided list with all chunks in the Ready state.
        ///     Clears the list before filling. Used for LOD level assignment.
        ///     Uses _readyChunks secondary index — O(ready count) not O(all loaded).
        /// </summary>
        public void FillReadyChunks(List<ManagedChunk> result)
        {
            _stateIndex.FillReadyChunks(result);
        }

        /// <summary>
        ///     Fills the provided list with all chunks that need cross-chunk light updates.
        ///     Iterates only the dirty set instead of all loaded chunks — O(dirty) not O(all).
        ///     Clears the list before filling.
        /// </summary>
        public void FillChunksNeedingLightUpdate(List<ManagedChunk> result)
        {
            _stateIndex.FillChunksNeedingLightUpdate(result, GetChunk);
        }

        /// <summary>
        ///     Marks a chunk as needing a cross-chunk light update and adds it to the dirty set.
        ///     Call this instead of setting chunk.NeedsLightUpdate = true directly.
        /// </summary>
        public void MarkNeedsLightUpdate(int3 coord)
        {
            _stateIndex.MarkNeedsLightUpdate(coord, GetChunk(coord));
        }

        /// <summary>
        ///     Clears the NeedsLightUpdate flag on a chunk and removes it from the dirty set.
        ///     Call this instead of setting chunk.NeedsLightUpdate = false directly.
        /// </summary>
        public void ClearNeedsLightUpdate(int3 coord)
        {
            _stateIndex.ClearNeedsLightUpdate(coord, GetChunk(coord));
        }

        /// <summary>
        ///     Fills the provided list with all chunks in the RelightPending state
        ///     that do not have a light job in-flight.
        ///     Uses _relightPendingChunks secondary index — O(relight count) not O(all loaded).
        /// </summary>
        public void FillChunksNeedingRelight(List<ManagedChunk> result)
        {
            _stateIndex.FillChunksNeedingRelight(result);
        }

        /// <summary>Returns the chunk at the given coordinate, or null if not loaded.</summary>
        public ManagedChunk GetChunk(int3 coord)
        {
            _chunks.TryGetValue(coord, out ManagedChunk chunk);

            return chunk;
        }

        /// <summary>
        ///     Returns true if the block at the given world coordinate is in a loaded,
        ///     sufficiently-generated chunk. Returns false if the chunk is missing,
        ///     still generating, or has no data.
        /// </summary>
        public bool IsBlockLoaded(int3 worldCoord)
        {
            return _editor.IsBlockLoaded(worldCoord);
        }

        /// <summary>
        ///     Gets the StateId at a world-space block coordinate.
        ///     Returns StateId.Air if the chunk is not loaded or not yet generated.
        /// </summary>
        public StateId GetBlock(int3 worldCoord)
        {
            return _editor.GetBlock(worldCoord);
        }

        /// <summary>
        ///     Sets the StateId at a world-space block coordinate.
        ///     Marks the chunk (and border-adjacent neighbors) for remeshing.
        ///     Sets the chunk to RelightPending so light is recalculated before remesh.
        ///     Does nothing if the chunk is not loaded or still generating.
        /// </summary>
        public void SetBlock(int3 worldCoord, StateId state, List<int3> dirtiedChunks)
        {
            _editor.SetBlock(worldCoord, state, dirtiedChunks);
        }

        /// <summary>
        ///     Applies deferred edits that arrived while a chunk was in Meshing state.
        ///     Writes the edits to ChunkData and fires block entity events for each edit.
        ///     Called by MeshScheduler.PollCompleted after the mesh job finishes.
        /// </summary>
        public void ApplyDeferredEdits(ManagedChunk chunk)
        {
            _editor.ApplyDeferredEdits(chunk);
        }

        /// <summary>
        ///     Converts a world-space block coordinate to a chunk coordinate.
        ///     Uses floor division to handle negative coordinates correctly.
        /// </summary>
        public static int3 WorldToChunk(int3 worldCoord)
        {
            return new int3(
                FloorDiv(worldCoord.x, ChunkConstants.Size),
                FloorDiv(worldCoord.y, ChunkConstants.Size),
                FloorDiv(worldCoord.z, ChunkConstants.Size));
        }

        /// <summary>Floor division that handles negative dividends correctly.</summary>
        private static int FloorDiv(int a, int b)
        {
            return a >= 0 ? a / b : (a - b + 1) / b;
        }

        /// <summary>
        ///     Unloads chunks outside all player interest regions, respecting refcounts
        ///     and grace periods. A chunk is eligible only when RefCount == 0 AND
        ///     GracePeriodExpiry has passed. Processes farthest-first within a time budget.
        /// </summary>
        public void UnloadDistantChunks(
            ReadOnlySpan<int3> playerChunkCoords,
            List<int3> unloaded,
            double currentRealtime,
            WorldStorage worldStorage = null,
            float budgetMs = 2.0f)
        {
            unloaded.Clear();

            _loadQueue.CollectUnloadCandidates(
                playerChunkCoords, currentRealtime, _chunks, _unloadCandidateCache);

            int candidateCount = _unloadCandidateCache.Count;

            if (candidateCount == 0)
            {
                return;
            }

            // Process within budget, farthest first (iterate in reverse)
            long startTicks = Stopwatch.GetTimestamp();
            double ticksPerMs = Stopwatch.Frequency / 1000.0;

            for (int i = candidateCount - 1; i >= 0; i--)
            {
                int3 coord = _unloadCandidateCache[i];

                if (!_chunks.TryGetValue(coord, out ManagedChunk chunk))
                {
                    continue;
                }

                chunk.ActiveJobHandle.Complete();

                // Notify block entities of unload
                if (chunk.BlockEntities is not null)
                {
                    foreach (KeyValuePair<int, IBlockEntity> bePair in chunk.BlockEntities)
                    {
                        bePair.Value.OnChunkUnload();
                    }
                }

                // Save modified chunks before unloading (enqueue must precede dispose)
                if (chunk.IsDirty && chunk.Data.IsCreated)
                {
                    if (_asyncSaver is not null)
                    {
                        _asyncSaver.EnqueueSave(
                            chunk.Coord, chunk.Data, chunk.LightData, chunk.BlockEntities);
                    }
                    else if (worldStorage is not null)
                    {
                        worldStorage.SaveChunk(
                            chunk.Coord, chunk.Data, chunk.LightData, chunk.BlockEntities);
                    }

                    chunk.IsDirty = false;
                }

                if (chunk.Data.IsCreated)
                {
                    _pool.Return(chunk.Data);

                    chunk.Data = default;
                }

                if (chunk.LightData.IsCreated)
                {
                    NativeArray<byte> ld = chunk.LightData;

                    ld.Dispose();

                    chunk.LightData = default;
                }

                if (chunk.HeightMap.IsCreated)
                {
                    NativeArray<int> hm = chunk.HeightMap;

                    hm.Dispose();

                    chunk.HeightMap = default;
                }

                if (chunk.RiverFlags.IsCreated)
                {
                    NativeArray<byte> rf = chunk.RiverFlags;

                    rf.Dispose();

                    chunk.RiverFlags = default;
                }

                unloaded.Add(coord);

                // Check budget
                double elapsedMs = (Stopwatch.GetTimestamp() - startTicks) / ticksPerMs;

                if (elapsedMs >= budgetMs)
                {
                    break;
                }
            }

            // Remove processed chunks from _chunks and index sets
            for (int i = 0; i < unloaded.Count; i++)
            {
                if (_chunks.TryGetValue(unloaded[i], out ManagedChunk unloadedChunk))
                {
                    UnregisterChunk(unloadedChunk);
                    _stateIndex.RemoveFromIndices(unloadedChunk, unloaded[i]);
                }

                RemoveChunk(unloaded[i], out _);
            }
        }

        /// <summary>
        ///     Single-player backward-compatible wrapper for
        ///     <see cref="UnloadDistantChunks(ReadOnlySpan{int3}, List{int3}, double, WorldStorage, float)" />.
        /// </summary>
        public void UnloadDistantChunks(
            int3 cameraChunkCoord,
            List<int3> unloaded,
            WorldStorage worldStorage = null,
            float budgetMs = 2.0f)
        {
            Span<int3> single = stackalloc int3[1];
            single[0] = cameraChunkCoord;
            // Pass currentRealtime=0 so all grace periods are treated as expired,
            // matching the legacy distance-only unload behavior.
            UnloadDistantChunks(
                (ReadOnlySpan<int3>)single, unloaded, 0.0, worldStorage, budgetMs);
        }

        /// <summary>
        ///     Fills the provided list with dirty chunks eligible for saving.
        ///     A chunk is eligible if IsDirty, State >= RelightPending, and Data.IsCreated.
        ///     Clears the list before filling. Caller uses fill pattern.
        /// </summary>
        public void CollectDirtyChunks(List<ManagedChunk> result)
        {
            result.Clear();

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                ManagedChunk chunk = kvp.Value;

                if (chunk.IsDirty && chunk.State >= ChunkState.RelightPending && chunk.Data.IsCreated)
                {
                    result.Add(chunk);
                }
            }
        }

        /// <summary>Synchronously saves all dirty chunks to storage (used at shutdown).</summary>
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

        /// <summary>
        ///     Loads a chunk from network-received voxel and light data.
        ///     Skips generation/decoration and transitions directly to Generated state
        ///     so the MeshScheduler can pick it up for meshing. Used by the client when
        ///     receiving <c>ChunkDataMessage</c> from the server.
        /// </summary>
        public void LoadFromNetwork(
            int3 coord,
            NativeArray<StateId> voxelData,
            NativeArray<byte> lightData)
        {
            if (_chunks.ContainsKey(coord))
            {
                // Already loaded — overwrite data and re-mesh
                ManagedChunk existing = _chunks[coord];
                existing.ActiveJobHandle.Complete();

                NativeArray<StateId>.Copy(voxelData, existing.Data);

                if (!existing.LightData.IsCreated)
                {
                    existing.LightData = new NativeArray<byte>(
                        ChunkConstants.Volume, Allocator.Persistent);
                }

                NativeArray<byte>.Copy(lightData, existing.LightData);

                // Transition to Generated so MeshScheduler picks it up
                SetChunkState(existing, ChunkState.Generated);
                return;
            }

            NativeArray<StateId> data = _pool.Checkout();
            NativeArray<StateId>.Copy(voxelData, data);

            ManagedChunk chunk = new(coord, data)
            {
                // Owner: ManagedChunk. Disposed by ChunkManager.UnloadChunk or UnloadDistantChunks.
                LightData = new NativeArray<byte>(
                    ChunkConstants.Volume, Allocator.Persistent),
            };
            NativeArray<byte>.Copy(lightData, chunk.LightData);

            AddChunk(coord, chunk);
            RegisterChunk(chunk);
            SetChunkState(chunk, ChunkState.Generated);
        }

        /// <summary>
        ///     Unloads a single chunk by coordinate. Used by the client when
        ///     the server sends a <c>ChunkUnloadMessage</c>. Does not save
        ///     the chunk (clients do not own world state).
        ///     Returns the coordinate list of unloaded chunks for mesh cleanup.
        /// </summary>
        public void UnloadChunk(int3 coord)
        {
            if (!_chunks.TryGetValue(coord, out ManagedChunk chunk))
            {
                return;
            }

            chunk.ActiveJobHandle.Complete();

            UnregisterChunk(chunk);
            _stateIndex.RemoveFromIndices(chunk, coord);

            if (chunk.Data.IsCreated)
            {
                _pool.Return(chunk.Data);

                chunk.Data = default;
            }

            if (chunk.LightData.IsCreated)
            {
                NativeArray<byte> ld = chunk.LightData;

                ld.Dispose();

                chunk.LightData = default;
            }

            if (chunk.HeightMap.IsCreated)
            {
                NativeArray<int> hm = chunk.HeightMap;

                hm.Dispose();

                chunk.HeightMap = default;
            }

            if (chunk.RiverFlags.IsCreated)
            {
                NativeArray<byte> rf = chunk.RiverFlags;

                rf.Dispose();

                chunk.RiverFlags = default;
            }

            RemoveChunk(coord, out _);
        }

        /// <summary>
        ///     Fills the given array with chunk counts per state. The array must have
        ///     length >= ChunkState enum count (8). Also outputs NeedsRemesh and
        ///     NeedsLightUpdate totals. Single iteration over _chunks.
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

        /// <summary>
        ///     Marks all Ready neighbors of the given coord as Generated (needing remesh),
        ///     and flags any Meshing neighbors for re-mesh after their current job completes.
        /// </summary>
        public void InvalidateReadyNeighbors(int3 coord)
        {
            ManagedChunk chunk = GetChunk(coord);

            if (chunk == null)
            {
                return;
            }

            for (int f = 0; f < 6; f++)
            {
                ManagedChunk neighbor = chunk.Neighbors[f];

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

        /// <summary>
        ///     Wires bidirectional Neighbors references and initializes ReadyNeighborMask
        ///     for a newly created chunk. Pre-sets bits for Y-boundary faces so that
        ///     world-edge chunks pass the "all neighbors" gate with fewer actual neighbors.
        ///     Must be called after the chunk is inserted into _chunks.
        /// </summary>
        private void RegisterChunk(ManagedChunk chunk)
        {
            // Pre-set bits for Y-boundary faces that will never have a neighbor.
            byte boundaryMask = 0;

            if (chunk.Coord.y <= _yLoadMin)
            {
                boundaryMask |= 1 << 3; // face 3 = -Y: no chunk below world range
            }

            if (chunk.Coord.y >= _yLoadMax)
            {
                boundaryMask |= 1 << 2; // face 2 = +Y: no chunk above world range
            }

            chunk.ReadyNeighborMask = boundaryMask;

            for (int f = 0; f < 6; f++)
            {
                int3 neighborCoord = chunk.Coord + s_neighborOffsets[f];

                if (!_chunks.TryGetValue(neighborCoord, out ManagedChunk neighbor))
                {
                    continue;
                }

                // Wire new chunk -> existing neighbor
                chunk.Neighbors[f] = neighbor;

                if (neighbor.State >= ChunkState.RelightPending)
                {
                    chunk.ReadyNeighborMask |= (byte)(1 << f);
                }

                // Wire existing neighbor -> new chunk (opposite face)
                int oppF = s_oppositeFace[f];
                neighbor.Neighbors[oppF] = chunk;

                // New chunk starts at Generating (< RelightPending), so don't set
                // the neighbor's bitmask bit yet — it will be set when this chunk
                // transitions to >= RelightPending via SetChunkState.
            }
        }

        /// <summary>
        ///     Clears bidirectional Neighbors references for a chunk being unloaded.
        ///     Clears the corresponding ReadyNeighborMask bit on each neighbor.
        ///     Must be called before the chunk is removed from _chunks.
        /// </summary>
        private void UnregisterChunk(ManagedChunk chunk)
        {
            for (int f = 0; f < 6; f++)
            {
                ManagedChunk neighbor = chunk.Neighbors[f];

                if (neighbor == null)
                {
                    continue;
                }

                int oppF = s_oppositeFace[f];
                neighbor.ReadyNeighborMask &= (byte)~(1 << oppF);
                neighbor.Neighbors[oppF] = null;
                chunk.Neighbors[f] = null;
            }
        }

        /// <summary>
        ///     Rebuilds the BorderFaceMask bitmask from the chunk's current BorderLightEntries.
        ///     Must be called after any modification to BorderLightEntries.
        /// </summary>
        public static void RebuildBorderFaceMask(ManagedChunk chunk)
        {
            byte mask = 0;

            for (int i = 0; i < chunk.BorderLightEntries.Count; i++)
            {
                mask |= (byte)(1 << chunk.BorderLightEntries[i].Face);
            }

            chunk.BorderFaceMask = mask;
        }

        /// <summary>
        ///     Returns the chunk at the given coordinate, or null if not loaded.
        ///     Alias for <see cref="GetChunk" /> for clarity in nullable-context usage.
        /// </summary>
        public ManagedChunk TryGetChunk(int3 coord)
        {
            _chunks.TryGetValue(coord, out ManagedChunk chunk);

            return chunk;
        }

        /// <summary>
        ///     Returns the first player chunk coordinate from the last UpdateLoadingQueue call.
        ///     Used by LiquidScheduler for determining liquid simulation radius.
        ///     Returns int3.zero if no player positions are available.
        /// </summary>
        public int3 GetCameraChunkCoord()
        {
            return _loadQueue.GetCameraChunkCoord();
        }

        /// <summary>
        ///     Fills the result list with chunks that have allocated LiquidData and are
        ///     within the given radius from the camera chunk. Only includes chunks in
        ///     Generated or Ready state.
        /// </summary>
        public void FillChunksWithLiquid(List<ManagedChunk> result, int3 cameraChunkCoord, int radius)
        {
            result.Clear();

            foreach (KeyValuePair<int3, ManagedChunk> kvp in _chunks)
            {
                ManagedChunk chunk = kvp.Value;

                if (!chunk.LiquidData.IsCreated)
                {
                    continue;
                }

                if (chunk.State < ChunkState.Generated)
                {
                    continue;
                }

                int3 diff = chunk.Coord - cameraChunkCoord;
                int chebyshev = math.max(math.abs(diff.x), math.max(math.abs(diff.y), math.abs(diff.z)));

                if (chebyshev <= radius)
                {
                    result.Add(chunk);
                }
            }
        }

        /// <summary>
        ///     Returns the liquid cell byte at a world-space block coordinate.
        ///     Returns 0 (empty) if the chunk is not loaded or has no liquid data.
        /// </summary>
        public byte GetFluidLevel(int3 worldCoord)
        {
            int3 chunkCoord = WorldToChunk(worldCoord);
            ManagedChunk chunk = GetChunk(chunkCoord);

            if (chunk == null || !chunk.LiquidData.IsCreated)
            {
                return 0;
            }

            // Complete any in-flight liquid job before reading LiquidData.
            // PlayerPhysicsBody ticks before LiquidScheduler.PollCompleted().
            chunk.LiquidJobHandle.Complete();

            int localX = worldCoord.x - chunkCoord.x * ChunkConstants.Size;
            int localY = worldCoord.y - chunkCoord.y * ChunkConstants.Size;
            int localZ = worldCoord.z - chunkCoord.z * ChunkConstants.Size;
            int index = ChunkData.GetIndex(localX, localY, localZ);

            return chunk.LiquidData[index];
        }
    }
}
