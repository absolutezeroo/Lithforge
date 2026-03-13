using System.Collections.Generic;
using Lithforge.Meshing;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Rendering;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.WorldGen.Lighting;
using Lithforge.WorldGen.Stages;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.Runtime.Scheduling
{
    /// <summary>
    /// Owns the mesh job queue: scheduling greedy mesh jobs for LOD0 chunks,
    /// polling for completion, uploading to ChunkMeshStore, and disposing resources.
    /// Also schedules async relight jobs for RelightPending chunks (two-phase pattern:
    /// schedule frame N, complete frame N+1).
    /// </summary>
    public sealed class MeshScheduler
    {
        private readonly List<PendingMesh> _pendingMeshes = new List<PendingMesh>();
        private readonly List<PendingMesh> _pendingDisposals = new List<PendingMesh>();
        private readonly ChunkManager _chunkManager;
        private readonly NativeStateRegistry _nativeStateRegistry;
        private readonly NativeAtlasLookup _nativeAtlasLookup;
        private readonly ChunkMeshStore _chunkMeshStore;
        private readonly ChunkCulling _culling;
        private readonly int _maxMeshesPerFrame;
        private readonly int _maxMeshCompletionsPerFrame;
        private readonly float _completionBudgetMs;

        /// <summary>
        /// Reusable list for FillChunksToMesh — avoids per-frame allocation.
        /// Owner: MeshScheduler. Lifetime: application.
        /// </summary>
        private readonly List<ManagedChunk> _meshCandidateCache = new List<ManagedChunk>();

        /// <summary>
        /// Reusable list for FillChunksNeedingRelight — avoids per-frame allocation.
        /// Owner: MeshScheduler. Lifetime: application.
        /// </summary>
        private readonly List<ManagedChunk> _relightCache = new List<ManagedChunk>();

        /// <summary>
        /// Relight jobs in flight, awaiting completion next frame.
        /// Two-phase pattern: schedule frame N, complete frame N+1.
        /// Owner: MeshScheduler. Lifetime: application.
        /// </summary>
        private readonly List<PendingRelight> _inFlightRelights = new List<PendingRelight>();

        /// <summary>
        /// Pool for List&lt;BorderLightEntry&gt; snapshots used during relight.
        /// Avoids per-relight allocation of the old border entry snapshot list.
        /// Owner: MeshScheduler. Lifetime: application.
        /// </summary>
        private readonly Stack<List<BorderLightEntry>> _borderEntryListPool = new Stack<List<BorderLightEntry>>();

        /// <summary>
        /// Face offsets for 6 cardinal directions: +X, -X, +Y, -Y, +Z, -Z.
        /// </summary>
        private static readonly int3[] _faceOffsets =
        {
            new int3(1, 0, 0),   // 0: +X
            new int3(-1, 0, 0),  // 1: -X
            new int3(0, 1, 0),   // 2: +Y
            new int3(0, -1, 0),  // 3: -Y
            new int3(0, 0, 1),   // 4: +Z
            new int3(0, 0, -1),  // 5: -Z
        };

        /// <summary>
        /// Maps face index to the opposite face: +X(0)↔-X(1), +Y(2)↔-Y(3), +Z(4)↔-Z(5).
        /// </summary>
        private static readonly int[] _oppositeFace = { 1, 0, 3, 2, 5, 4 };

        public int PendingCount
        {
            get { return _pendingMeshes.Count; }
        }

        public MeshScheduler(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            NativeAtlasLookup nativeAtlasLookup,
            ChunkMeshStore chunkMeshStore,
            ChunkCulling culling,
            int maxMeshesPerFrame,
            int maxMeshCompletionsPerFrame,
            float completionBudgetMs)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
            _nativeAtlasLookup = nativeAtlasLookup;
            _chunkMeshStore = chunkMeshStore;
            _culling = culling;
            _maxMeshesPerFrame = maxMeshesPerFrame;
            _maxMeshCompletionsPerFrame = maxMeshCompletionsPerFrame;
            _completionBudgetMs = completionBudgetMs;
        }

        public void PollCompleted()
        {
            long freq = System.Diagnostics.Stopwatch.Frequency;

            long td0 = System.Diagnostics.Stopwatch.GetTimestamp();
            PollPendingDisposals();
            long td1 = System.Diagnostics.Stopwatch.GetTimestamp();
            PipelineStats.PollMeshDisposalsMs = (float)((td1 - td0) * 1000.0 / freq);

            long tr0 = System.Diagnostics.Stopwatch.GetTimestamp();
            PollRelightCompleted();
            long tr1 = System.Diagnostics.Stopwatch.GetTimestamp();
            PipelineStats.PollMeshRelightMs = (float)((tr1 - tr0) * 1000.0 / freq);

            FrameBudget budget = new FrameBudget(_completionBudgetMs);
            int completedThisFrame = 0;
            int i = 0;
            float uploadAccum = 0f;
            bool firstCheck = true;

            while (i < _pendingMeshes.Count)
            {
                if (completedThisFrame >= _maxMeshCompletionsPerFrame || budget.IsExhausted())
                {
                    break;
                }

                PendingMesh pending = _pendingMeshes[i];

                bool isCompleted;

                if (firstCheck)
                {
                    long tf0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    isCompleted = pending.Handle.IsCompleted;
                    long tf1 = System.Diagnostics.Stopwatch.GetTimestamp();
                    PipelineStats.PollMeshFirstIsCompletedMs = (float)((tf1 - tf0) * 1000.0 / freq);
                    firstCheck = false;
                }
                else
                {
                    isCompleted = pending.Handle.IsCompleted;
                }

                if (isCompleted)
                {
                    completedThisFrame++;
                    long tc0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    pending.Handle.Complete();
                    long tc1 = System.Diagnostics.Stopwatch.GetTimestamp();
                    float completeMs = (float)((tc1 - tc0) * 1000.0 / freq);
                    PipelineStats.RecordMeshComplete(completeMs);

                    long tu0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    _chunkMeshStore.UpdateRenderer(
                        pending.Coord,
                        pending.Data.OpaqueVertices,
                        pending.Data.OpaqueIndices,
                        pending.Data.CutoutVertices,
                        pending.Data.CutoutIndices,
                        pending.Data.TranslucentVertices,
                        pending.Data.TranslucentIndices);
                    long tu1 = System.Diagnostics.Stopwatch.GetTimestamp();
                    uploadAccum += (float)((tu1 - tu0) * 1000.0 / freq);

                    pending.Data.Dispose();
                    PipelineStats.IncrMeshCompleted();

                    ManagedChunk chunk = _chunkManager.GetChunk(pending.Coord);

                    if (chunk != null)
                    {
                        if (chunk.DeferredEdits.Count > 0)
                        {
                            // Apply deferred edits that arrived while the mesh job was in-flight.
                            // This writes to ChunkData now that the job is complete and no
                            // worker thread is reading it.
                            NativeArray<StateId> chunkData = chunk.Data;

                            for (int di = 0; di < chunk.DeferredEdits.Count; di++)
                            {
                                DeferredEdit edit = chunk.DeferredEdits[di];
                                chunkData[edit.FlatIndex] = edit.NewState;
                                chunk.PendingEditIndices.Add(edit.FlatIndex);
                            }

                            chunk.DeferredEdits.Clear();
                            chunk.State = ChunkState.RelightPending;
                        }
                        else if (chunk.NeedsRemesh)
                        {
                            chunk.NeedsRemesh = false;
                            chunk.State = ChunkState.Generated;
                        }
                        else
                        {
                            chunk.State = ChunkState.Ready;
                        }

                        chunk.ActiveJobHandle = default;
                        chunk.RenderedLODLevel = 0;
                    }

                    // Swap-back: O(1) removal instead of O(n) RemoveAt shift
                    int last = _pendingMeshes.Count - 1;

                    if (i < last)
                    {
                        _pendingMeshes[i] = _pendingMeshes[last];
                    }

                    _pendingMeshes.RemoveAt(last);
                    // Do not increment i — recheck position (now holds old last element)
                }
                else
                {
                    i++;
                }
            }

            PipelineStats.PollMeshUploadMs = uploadAccum;

            // Iterate time = residual (total method time minus all measured sub-parts)
            long tEnd = System.Diagnostics.Stopwatch.GetTimestamp();
            float totalMethodMs = (float)((tEnd - td0) * 1000.0 / freq);
            PipelineStats.PollMeshIterateMs = totalMethodMs
                - PipelineStats.PollMeshDisposalsMs
                - PipelineStats.PollMeshRelightMs
                - uploadAccum;
            // Note: Complete() time and FirstIsCompleted time are included in IterateMs.
            // This is intentional — IterateMs captures "everything else in the while loop".
        }

        /// <summary>
        /// Schedules async relight jobs for RelightPending chunks.
        /// Two-phase pattern: jobs are kicked to worker threads here and completed
        /// in PollRelightCompleted() next frame. Chunks stay in RelightPending state
        /// until the relight job completes, which gates meshing automatically (since
        /// FillChunksToMesh only returns Generated chunks).
        /// Uses targeted changed indices from PendingEditIndices and border removal
        /// seeds from PendingBorderRemovals instead of scanning all 32768 voxels.
        /// Should be called each frame before ScheduleJobs().
        /// </summary>
        public void ScheduleRelightJobs()
        {
            _chunkManager.FillChunksNeedingRelight(_relightCache);

            if (_relightCache.Count == 0)
            {
                return;
            }

            bool scheduled = false;

            for (int i = 0; i < _relightCache.Count; i++)
            {
                ManagedChunk chunk = _relightCache[i];

                if (!chunk.LightData.IsCreated || !chunk.Data.IsCreated)
                {
                    chunk.State = ChunkState.Generated;
                    continue;
                }

                if (IsRelightInFlight(chunk))
                {
                    continue;
                }

                // Build targeted changed indices from pending edits
                NativeArray<int> changedIndices = new NativeArray<int>(
                    chunk.PendingEditIndices.Count, Allocator.TempJob);

                for (int j = 0; j < chunk.PendingEditIndices.Count; j++)
                {
                    changedIndices[j] = chunk.PendingEditIndices[j];
                }

                chunk.PendingEditIndices.Clear();

                // Build border removal seeds from pending cross-chunk cascade
                NativeArray<NativeBorderLightEntry> borderRemovalSeeds =
                    new NativeArray<NativeBorderLightEntry>(
                        chunk.PendingBorderRemovals.Count, Allocator.TempJob);

                for (int j = 0; j < chunk.PendingBorderRemovals.Count; j++)
                {
                    BorderLightEntry entry = chunk.PendingBorderRemovals[j];
                    borderRemovalSeeds[j] = new NativeBorderLightEntry
                    {
                        LocalPosition = entry.LocalPosition,
                        PackedLight = entry.PackedLight,
                        Face = entry.Face,
                    };
                }

                chunk.PendingBorderRemovals.Clear();

                // Allocate border light output for post-relight border scanning
                NativeList<NativeBorderLightEntry> borderLightOutput =
                    new NativeList<NativeBorderLightEntry>(256, Allocator.TempJob);

                // Snapshot old border entries for diffing after job completes
                List<BorderLightEntry> oldBorderEntries;

                if (_borderEntryListPool.Count > 0)
                {
                    oldBorderEntries = _borderEntryListPool.Pop();
                    oldBorderEntries.Clear();
                }
                else
                {
                    oldBorderEntries = new List<BorderLightEntry>();
                }

                oldBorderEntries.AddRange(chunk.BorderLightEntries);

                LightRemovalJob removalJob = new LightRemovalJob
                {
                    LightData = chunk.LightData,
                    ChunkData = chunk.Data,
                    StateTable = _nativeStateRegistry.States,
                    HeightMap = chunk.HeightMap,
                    ChunkWorldY = chunk.Coord.y * ChunkConstants.Size,
                    ChangedIndices = changedIndices,
                    BorderRemovalSeeds = borderRemovalSeeds,
                    BorderLightOutput = borderLightOutput,
                };

                JobHandle handle = removalJob.Schedule();

                chunk.ActiveJobHandle = handle;
                chunk.LightJobInFlight = true;

                _inFlightRelights.Add(new PendingRelight
                {
                    Chunk = chunk,
                    Handle = handle,
                    FrameAge = 0,
                    ChangedIndices = changedIndices,
                    BorderRemovalSeeds = borderRemovalSeeds,
                    BorderLightOutput = borderLightOutput,
                    OldBorderEntries = oldBorderEntries,
                });

                scheduled = true;
            }

            if (scheduled)
            {
                JobHandle.ScheduleBatchedJobs();
            }
        }

        /// <summary>
        /// Schedules greedy mesh jobs for Generated chunks.
        /// When applyFilters is true, LOD0-only filtering is applied and frustum chunks
        /// are prioritized (but off-frustum chunks still mesh if budget remains).
        /// During spawn (applyFilters=false), all Generated chunks are meshed unconditionally.
        /// </summary>
        public void ScheduleJobs(bool applyFilters)
        {
            int slotsAvailable = _maxMeshesPerFrame - _pendingMeshes.Count;

            if (slotsAvailable <= 0)
            {
                return;
            }

            // Request extra candidates when filtering, since some will be filtered by LOD
            int candidateCount = applyFilters ? slotsAvailable * 2 : slotsAvailable;
            _chunkManager.FillChunksToMesh(_meshCandidateCache, candidateCount);

            if (applyFilters)
            {
                // Remove non-LOD0 chunks (LOD chunks are handled by LODScheduler)
                for (int i = _meshCandidateCache.Count - 1; i >= 0; i--)
                {
                    if (_meshCandidateCache[i].LODLevel > 0)
                    {
                        _meshCandidateCache.RemoveAt(i);
                    }
                }

                // Partition: frustum chunks first, then the rest.
                // Stable for the front group (preserves neighbor-count priority).
                int writeIdx = 0;

                for (int i = 0; i < _meshCandidateCache.Count; i++)
                {
                    ManagedChunk c = _meshCandidateCache[i];

                    if (_culling.IsInFrustum(c.Coord))
                    {
                        _meshCandidateCache[i] = _meshCandidateCache[writeIdx];
                        _meshCandidateCache[writeIdx] = c;
                        writeIdx++;
                    }
                }
            }

            // Schedule up to slotsAvailable from the prioritized list
            int scheduleCount = _meshCandidateCache.Count < slotsAvailable
                ? _meshCandidateCache.Count
                : slotsAvailable;

            for (int i = 0; i < scheduleCount; i++)
            {
                ManagedChunk chunk = _meshCandidateCache[i];

                chunk.State = ChunkState.Meshing;

                GreedyMeshData meshData = new GreedyMeshData(Allocator.TempJob);

                // Schedule border extraction jobs on worker threads (Burst-compiled)
                JobHandle borderDependency = ScheduleBorderExtractionJobs(chunk.Coord, meshData);

                GreedyMeshJob meshJob = new GreedyMeshJob
                {
                    ChunkData = chunk.Data,
                    NeighborPosX = meshData.NeighborPosX,
                    NeighborNegX = meshData.NeighborNegX,
                    NeighborPosY = meshData.NeighborPosY,
                    NeighborNegY = meshData.NeighborNegY,
                    NeighborPosZ = meshData.NeighborPosZ,
                    NeighborNegZ = meshData.NeighborNegZ,
                    StateTable = _nativeStateRegistry.States,
                    AtlasEntries = _nativeAtlasLookup.Entries,
                    LightData = chunk.LightData,
                    OpaqueVertices = meshData.OpaqueVertices,
                    OpaqueIndices = meshData.OpaqueIndices,
                    CutoutVertices = meshData.CutoutVertices,
                    CutoutIndices = meshData.CutoutIndices,
                    TranslucentVertices = meshData.TranslucentVertices,
                    TranslucentIndices = meshData.TranslucentIndices,
                };

                JobHandle meshHandle = meshJob.Schedule(borderDependency);
                chunk.ActiveJobHandle = meshHandle;

                _pendingMeshes.Add(new PendingMesh
                {
                    Coord = chunk.Coord,
                    Handle = meshHandle,
                    Data = meshData,
                });
                PipelineStats.IncrMeshScheduled();
            }

            if (scheduleCount > 0)
            {
                JobHandle.ScheduleBatchedJobs();
            }
        }

        public void CleanupCoord(int3 coord)
        {
            for (int i = _pendingMeshes.Count - 1; i >= 0; i--)
            {
                if (_pendingMeshes[i].Coord.Equals(coord))
                {
                    _pendingDisposals.Add(_pendingMeshes[i]);
                    _pendingMeshes.RemoveAt(i);
                }
            }

            for (int i = _inFlightRelights.Count - 1; i >= 0; i--)
            {
                if (_inFlightRelights[i].Chunk.Coord.Equals(coord))
                {
                    _inFlightRelights[i].Handle.Complete();
                    _inFlightRelights[i].Chunk.LightJobInFlight = false;
                    _inFlightRelights[i].ChangedIndices.Dispose();
                    _inFlightRelights[i].BorderRemovalSeeds.Dispose();
                    _inFlightRelights[i].BorderLightOutput.Dispose();
                    _borderEntryListPool.Push(_inFlightRelights[i].OldBorderEntries);
                    _inFlightRelights.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Force-completes any pending mesh or disposal jobs whose border extraction
        /// reads the given coord's chunk data. Must be called before the chunk's
        /// NativeArray is returned to the pool so that Unity's job safety locks are
        /// released. The jobs remain in their lists for normal processing.
        /// Checks IsCompleted first to avoid work-stealing on unrelated jobs.
        /// </summary>
        public void ForceCompleteNeighborDeps(int3 neighborCoord)
        {
            for (int i = 0; i < _pendingMeshes.Count; i++)
            {
                if (IsAdjacentCoord(_pendingMeshes[i].Coord, neighborCoord))
                {
                    _pendingMeshes[i].Handle.Complete();
                }
            }

            for (int i = 0; i < _pendingDisposals.Count; i++)
            {
                if (IsAdjacentCoord(_pendingDisposals[i].Coord, neighborCoord))
                {
                    _pendingDisposals[i].Handle.Complete();
                }
            }
        }

        private static bool IsAdjacentCoord(int3 a, int3 b)
        {
            int3 d = a - b;
            int manhattan = math.abs(d.x) + math.abs(d.y) + math.abs(d.z);

            return manhattan == 1;
        }

        public void Shutdown()
        {
            for (int i = 0; i < _pendingMeshes.Count; i++)
            {
                _pendingMeshes[i].Handle.Complete();
                _pendingMeshes[i].Data.Dispose();
            }

            _pendingMeshes.Clear();

            for (int i = 0; i < _pendingDisposals.Count; i++)
            {
                _pendingDisposals[i].Handle.Complete();
                _pendingDisposals[i].Data.Dispose();
            }

            _pendingDisposals.Clear();

            for (int i = 0; i < _inFlightRelights.Count; i++)
            {
                _inFlightRelights[i].Handle.Complete();
                _inFlightRelights[i].ChangedIndices.Dispose();
                _inFlightRelights[i].BorderRemovalSeeds.Dispose();
                _inFlightRelights[i].BorderLightOutput.Dispose();
            }

            _inFlightRelights.Clear();
        }

        /// <summary>
        /// Completes in-flight relight jobs from last frame. Jobs that completed on
        /// worker threads transition their chunk from RelightPending to Generated.
        /// After completion, compares old vs new border light entries to cascade
        /// light changes to neighboring chunks (both removal and increase paths).
        /// FrameAge >= 3 forces completion as a TempJob safety fallback (matches
        /// the pattern in GenerationScheduler.ProcessCrossChunkLightUpdates).
        /// </summary>
        private void PollRelightCompleted()
        {
            for (int i = _inFlightRelights.Count - 1; i >= 0; i--)
            {
                PendingRelight entry = _inFlightRelights[i];

                if (entry.Handle.IsCompleted || entry.FrameAge >= 3)
                {
                    entry.Handle.Complete();

                    // Process border cascade before transitioning state
                    ProcessRelightBorderCascade(entry);

                    entry.Chunk.LightJobInFlight = false;
                    entry.Chunk.ActiveJobHandle = default;

                    // If border removal seeds were accumulated while this job was in
                    // flight (from a neighbor's cascade), immediately re-enter
                    // RelightPending so they are processed next frame.
                    if (entry.Chunk.PendingBorderRemovals.Count > 0 ||
                        entry.Chunk.PendingEditIndices.Count > 0)
                    {
                        entry.Chunk.State = ChunkState.RelightPending;
                    }
                    else
                    {
                        entry.Chunk.State = ChunkState.Generated;
                    }

                    // Dispose native containers from this relight
                    entry.ChangedIndices.Dispose();
                    entry.BorderRemovalSeeds.Dispose();
                    entry.BorderLightOutput.Dispose();

                    // Return pooled list for reuse
                    _borderEntryListPool.Push(entry.OldBorderEntries);

                    _inFlightRelights.RemoveAt(i);
                }
                else
                {
                    entry.FrameAge++;
                    _inFlightRelights[i] = entry;
                }
            }
        }

        /// <summary>
        /// After a LightRemovalJob completes, compares the chunk's old border light
        /// entries with the new post-relight border state. For any face where light
        /// decreased, creates removal seeds for the neighbor chunk (setting it to
        /// RelightPending). For any face where light increased, marks the neighbor
        /// for LightUpdateJob (NeedsLightUpdate). Also checks if THIS chunk should
        /// receive incoming light from neighbors (e.g., sunlight column restoration
        /// from the chunk above after block removal).
        /// </summary>
        private void ProcessRelightBorderCascade(PendingRelight entry)
        {
            ManagedChunk chunk = entry.Chunk;
            List<BorderLightEntry> oldEntries = entry.OldBorderEntries;
            NativeList<NativeBorderLightEntry> newOutput = entry.BorderLightOutput;

            // Update chunk's border entries with post-relight values
            chunk.BorderLightEntries.Clear();

            for (int i = 0; i < newOutput.Length; i++)
            {
                NativeBorderLightEntry native = newOutput[i];
                chunk.BorderLightEntries.Add(new BorderLightEntry
                {
                    LocalPosition = native.LocalPosition,
                    PackedLight = native.PackedLight,
                    Face = native.Face,
                });
            }

            // For each face, compare old vs new border entries and cascade to neighbors
            for (int f = 0; f < 6; f++)
            {
                int3 neighborCoord = chunk.Coord + _faceOffsets[f];
                ManagedChunk neighbor = _chunkManager.GetChunk(neighborCoord);

                if (neighbor == null ||
                    neighbor.State < ChunkState.RelightPending ||
                    !neighbor.LightData.IsCreated)
                {
                    continue;
                }

                bool hasDecreased = false;
                bool hasIncreased = false;

                // Check for decreases: old entries whose light is now lower at the same position
                for (int oi = 0; oi < oldEntries.Count; oi++)
                {
                    if (oldEntries[oi].Face != f)
                    {
                        continue;
                    }

                    int3 pos = oldEntries[oi].LocalPosition;
                    int idx = Lithforge.Voxel.Chunk.ChunkData.GetIndex(pos.x, pos.y, pos.z);
                    byte newPacked = chunk.LightData[idx];
                    byte oldPacked = oldEntries[oi].PackedLight;

                    byte oldSun = LightUtils.GetSunLight(oldPacked);
                    byte oldBlock = LightUtils.GetBlockLight(oldPacked);
                    byte newSun = LightUtils.GetSunLight(newPacked);
                    byte newBlock = LightUtils.GetBlockLight(newPacked);

                    if (newSun < oldSun || newBlock < oldBlock)
                    {
                        hasDecreased = true;
                        int3 neighborLocal = MapBorderToNeighborLocal(pos, f);
                        neighbor.PendingBorderRemovals.Add(new BorderLightEntry
                        {
                            LocalPosition = neighborLocal,
                            PackedLight = oldPacked,
                            Face = (byte)_oppositeFace[f],
                        });
                    }
                }

                // Check for increases: new entries with higher light than old at the same position
                for (int ni = 0; ni < chunk.BorderLightEntries.Count; ni++)
                {
                    if (chunk.BorderLightEntries[ni].Face != f)
                    {
                        continue;
                    }

                    byte newPacked = chunk.BorderLightEntries[ni].PackedLight;
                    int3 pos = chunk.BorderLightEntries[ni].LocalPosition;

                    // Find corresponding old entry
                    bool foundOld = false;

                    for (int oj = 0; oj < oldEntries.Count; oj++)
                    {
                        if (oldEntries[oj].Face == f &&
                            oldEntries[oj].LocalPosition.x == pos.x &&
                            oldEntries[oj].LocalPosition.y == pos.y &&
                            oldEntries[oj].LocalPosition.z == pos.z)
                        {
                            foundOld = true;
                            byte oldPacked = oldEntries[oj].PackedLight;

                            if (LightUtils.GetSunLight(newPacked) > LightUtils.GetSunLight(oldPacked) ||
                                LightUtils.GetBlockLight(newPacked) > LightUtils.GetBlockLight(oldPacked))
                            {
                                hasIncreased = true;
                            }

                            break;
                        }
                    }

                    if (!foundOld)
                    {
                        hasIncreased = true;
                    }

                    if (hasIncreased)
                    {
                        break;
                    }
                }

                // Cascade decreases: set neighbor to RelightPending for removal BFS
                if (hasDecreased &&
                    neighbor.State >= ChunkState.Generated &&
                    !neighbor.LightJobInFlight)
                {
                    if (neighbor.State == ChunkState.Meshing)
                    {
                        neighbor.ActiveJobHandle.Complete();
                    }

                    neighbor.State = ChunkState.RelightPending;
                }

                // Cascade increases: mark neighbor for LightUpdateJob
                if (hasIncreased)
                {
                    neighbor.NeedsLightUpdate = true;
                }
            }

            // Check if THIS chunk should receive incoming light from neighbors
            // (e.g., sunlight column restoration from the chunk above after block removal,
            // or torch light from a neighboring chunk after this chunk's border cleared)
            for (int f = 0; f < 6; f++)
            {
                int3 neighborCoord = chunk.Coord + _faceOffsets[f];
                ManagedChunk neighbor = _chunkManager.GetChunk(neighborCoord);

                if (neighbor == null || neighbor.BorderLightEntries.Count == 0)
                {
                    continue;
                }

                int oppFace = _oppositeFace[f];

                for (int ni = 0; ni < neighbor.BorderLightEntries.Count; ni++)
                {
                    if (neighbor.BorderLightEntries[ni].Face == oppFace)
                    {
                        chunk.NeedsLightUpdate = true;

                        break;
                    }
                }

                if (chunk.NeedsLightUpdate)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Maps a border voxel's local position from the source chunk to the
        /// receiving chunk's local coordinate system.
        /// </summary>
        private static int3 MapBorderToNeighborLocal(int3 sourceLocal, int sourceFace)
        {
            int lastIdx = ChunkConstants.Size - 1;

            switch (sourceFace)
            {
                case 0: // +X face of source -> -X face of receiver (x=0)
                    return new int3(0, sourceLocal.y, sourceLocal.z);
                case 1: // -X face of source -> +X face of receiver (x=31)
                    return new int3(lastIdx, sourceLocal.y, sourceLocal.z);
                case 2: // +Y face of source -> -Y face of receiver (y=0)
                    return new int3(sourceLocal.x, 0, sourceLocal.z);
                case 3: // -Y face of source -> +Y face of receiver (y=31)
                    return new int3(sourceLocal.x, lastIdx, sourceLocal.z);
                case 4: // +Z face of source -> -Z face of receiver (z=0)
                    return new int3(sourceLocal.x, sourceLocal.y, 0);
                case 5: // -Z face of source -> +Z face of receiver (z=31)
                    return new int3(sourceLocal.x, sourceLocal.y, lastIdx);
                default:
                    return sourceLocal;
            }
        }

        private bool IsRelightInFlight(ManagedChunk chunk)
        {
            for (int i = 0; i < _inFlightRelights.Count; i++)
            {
                if (_inFlightRelights[i].Chunk.Coord.Equals(chunk.Coord))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Disposes completed mesh jobs that were deferred by CleanupCoord.
        /// Budgeted: disposes at most 2 items per frame to avoid burst spikes
        /// when many chunks are unloaded simultaneously.
        /// </summary>
        private void PollPendingDisposals()
        {
            int disposed = 0;
            int i = 0;

            while (i < _pendingDisposals.Count)
            {
                if (disposed >= 2)
                {
                    break;
                }

                if (_pendingDisposals[i].Handle.IsCompleted)
                {
                    _pendingDisposals[i].Handle.Complete();
                    _pendingDisposals[i].Data.Dispose();
                    disposed++;

                    // Swap-back O(1) removal
                    int last = _pendingDisposals.Count - 1;

                    if (i < last)
                    {
                        _pendingDisposals[i] = _pendingDisposals[last];
                    }

                    _pendingDisposals.RemoveAt(last);
                }
                else
                {
                    i++;
                }
            }
        }

        /// <summary>
        /// Neighbor offset + face direction pairs for border extraction.
        /// Each entry: (offset to neighbor chunk, face direction to extract from that neighbor).
        /// </summary>
        private static readonly (int3 Offset, int Face, int OutputIndex)[] _borderExtractions =
        {
            (new int3(1, 0, 0),  1, 0), // +X neighbor: extract its -X face
            (new int3(-1, 0, 0), 0, 1), // -X neighbor: extract its +X face
            (new int3(0, 1, 0),  3, 2), // +Y neighbor: extract its -Y face
            (new int3(0, -1, 0), 2, 3), // -Y neighbor: extract its +Y face
            (new int3(0, 0, 1),  5, 4), // +Z neighbor: extract its -Z face
            (new int3(0, 0, -1), 4, 5), // -Z neighbor: extract its +Z face
        };

        /// <summary>
        /// Schedules up to 6 Burst-compiled ExtractSingleBorderJob jobs on worker threads.
        /// Returns a combined JobHandle that the GreedyMeshJob must depend on.
        /// Neighbors that don't exist or aren't generated leave the output zero-initialized (air).
        /// </summary>
        private JobHandle ScheduleBorderExtractionJobs(int3 coord, GreedyMeshData meshData)
        {
            int handleCount = 0;
            NativeArray<JobHandle> handles = new NativeArray<JobHandle>(6, Allocator.Temp, NativeArrayOptions.ClearMemory);

            for (int i = 0; i < _borderExtractions.Length; i++)
            {
                int3 neighborCoord = coord + _borderExtractions[i].Offset;
                ManagedChunk neighbor = _chunkManager.GetChunk(neighborCoord);

                if (neighbor == null || neighbor.State < ChunkState.RelightPending || !neighbor.Data.IsCreated)
                {
                    continue;
                }

                ExtractSingleBorderJob borderJob = new ExtractSingleBorderJob
                {
                    ChunkData = neighbor.Data,
                    FaceDirection = _borderExtractions[i].Face,
                    Output = GetBorderOutput(meshData, _borderExtractions[i].OutputIndex),
                };

                handles[handleCount] = borderJob.Schedule();
                handleCount++;
            }

            JobHandle combined;

            if (handleCount == 0)
            {
                combined = default;
            }
            else
            {
                combined = JobHandle.CombineDependencies(handles);
            }

            handles.Dispose();

            return combined;
        }

        private static NativeArray<StateId> GetBorderOutput(GreedyMeshData meshData, int outputIndex)
        {
            switch (outputIndex)
            {
                case 0: return meshData.NeighborPosX;
                case 1: return meshData.NeighborNegX;
                case 2: return meshData.NeighborPosY;
                case 3: return meshData.NeighborNegY;
                case 4: return meshData.NeighborPosZ;
                default: return meshData.NeighborNegZ;
            }
        }

        private struct PendingMesh
        {
            public int3 Coord;
            public JobHandle Handle;
            public GreedyMeshData Data;
        }

        private struct PendingRelight
        {
            public ManagedChunk Chunk;
            public JobHandle Handle;
            public int FrameAge;
            public NativeArray<int> ChangedIndices;
            public NativeArray<NativeBorderLightEntry> BorderRemovalSeeds;
            public NativeList<NativeBorderLightEntry> BorderLightOutput;
            public List<BorderLightEntry> OldBorderEntries;
        }
    }
}
