using System.Collections.Generic;
using Lithforge.Meshing;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Rendering;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
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
        /// Shared index array for LightRemovalJob (all 32768 voxel indices).
        /// Allocated once on first use with Allocator.Persistent because it is
        /// shared across frames (TempJob would expire after 4 frames).
        /// Owner: MeshScheduler. Disposed in Shutdown().
        /// </summary>
        private NativeArray<int> _relightAllIndices;

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
                        if (chunk.NeedsRemesh)
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
        /// Should be called each frame before ScheduleJobs().
        /// </summary>
        public void ScheduleRelightJobs()
        {
            _chunkManager.FillChunksNeedingRelight(_relightCache);

            if (_relightCache.Count == 0)
            {
                return;
            }

            // Lazy-allocate shared index array on first use
            if (!_relightAllIndices.IsCreated)
            {
                _relightAllIndices = new NativeArray<int>(ChunkConstants.Volume, Allocator.Persistent);

                for (int idx = 0; idx < ChunkConstants.Volume; idx++)
                {
                    _relightAllIndices[idx] = idx;
                }
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

                LightRemovalJob removalJob = new LightRemovalJob
                {
                    LightData = chunk.LightData,
                    ChunkData = chunk.Data,
                    StateTable = _nativeStateRegistry.States,
                    ChangedIndices = _relightAllIndices,
                };

                JobHandle handle = removalJob.Schedule();

                chunk.LightJobInFlight = true;

                _inFlightRelights.Add(new PendingRelight
                {
                    Chunk = chunk,
                    Handle = handle,
                    FrameAge = 0,
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
            }

            _inFlightRelights.Clear();

            if (_relightAllIndices.IsCreated)
            {
                _relightAllIndices.Dispose();
            }
        }

        /// <summary>
        /// Completes in-flight relight jobs from last frame. Jobs that completed on
        /// worker threads transition their chunk from RelightPending to Generated.
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
                    entry.Chunk.State = ChunkState.Generated;
                    entry.Chunk.LightJobInFlight = false;
                    _inFlightRelights.RemoveAt(i);
                }
                else
                {
                    entry.FrameAge++;
                    _inFlightRelights[i] = entry;
                }
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
        /// Drains the lazy disposal queue. Jobs that were in-flight when their chunk was
        /// unloaded are polled here — once complete, their TempJob data is disposed.
        /// Called at the start of PollCompleted each frame.
        /// </summary>
        private void PollPendingDisposals()
        {
            for (int i = _pendingDisposals.Count - 1; i >= 0; i--)
            {
                if (_pendingDisposals[i].Handle.IsCompleted)
                {
                    _pendingDisposals[i].Handle.Complete();
                    _pendingDisposals[i].Data.Dispose();
                    _pendingDisposals.RemoveAt(i);
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
        }
    }
}
