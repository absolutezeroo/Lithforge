using System.Collections.Generic;
using Lithforge.Meshing;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Rendering;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Profiling;

namespace Lithforge.Runtime.Scheduling
{
    /// <summary>
    /// Owns the mesh job queue: scheduling greedy mesh jobs for LOD0 chunks,
    /// polling for completion, uploading to ChunkMeshStore, and disposing resources.
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
        private int _maxMeshesPerFrame;
        private int _maxMeshCompletionsPerFrame;
        private int _throttleThreshold = 16;
        private readonly float _completionBudgetMs;

        /// <summary>
        /// Dummy NativeArray passed to ExtractAllBordersJob for missing neighbors.
        /// The job's HasXxx flags prevent reading from it; this satisfies the safety system.
        /// Owner: MeshScheduler. Lifetime: application. Allocator: Persistent.
        /// </summary>
        private NativeArray<StateId> _dummyBorder;

        /// <summary>
        /// Reusable list for FillChunksToMesh — avoids per-frame allocation.
        /// Owner: MeshScheduler. Lifetime: application.
        /// </summary>
        private readonly List<ManagedChunk> _meshCandidateCache = new List<ManagedChunk>();

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
            _dummyBorder = new NativeArray<StateId>(1, Allocator.Persistent);
        }

        public void UpdateConfig(int renderDistance)
        {
            _maxMeshesPerFrame = SchedulingConfig.MaxMeshesPerFrame(renderDistance);
            _maxMeshCompletionsPerFrame = SchedulingConfig.MaxMeshCompletionsPerFrame(renderDistance);
            _throttleThreshold = SchedulingConfig.ThrottleThreshold(renderDistance);
        }

        public void PollCompleted()
        {
            Profiler.BeginSample("MS.PollCompleted");

            long freq = System.Diagnostics.Stopwatch.Frequency;

            long td0 = System.Diagnostics.Stopwatch.GetTimestamp();
            PollPendingDisposals();
            long td1 = System.Diagnostics.Stopwatch.GetTimestamp();
            PipelineStats.PollMeshDisposalsMs = (float)((td1 - td0) * 1000.0 / freq);

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

                // Don't poll IsCompleted on jobs younger than 1 frame.
                // Avoids Unity work-stealing the job + its border extraction deps
                // on the main thread (measured: up to 134ms stalls).
                bool forceComplete = false;

                if (pending.FrameAge > 300)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[MeshScheduler] Force-completing stale mesh job for chunk " +
                        $"{pending.Coord} after {pending.FrameAge} frames");
                    forceComplete = true;
                }
                else if (pending.FrameAge < 1)
                {
                    pending.FrameAge++;
                    _pendingMeshes[i] = pending;
                    i++;
                    continue;
                }

                bool isCompleted;

                if (forceComplete)
                {
                    isCompleted = true;
                }
                else if (firstCheck)
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
                    Profiler.BeginSample("MS.GPUUpload");
                    _chunkMeshStore.UpdateRenderer(
                        pending.Coord,
                        pending.Data.OpaqueVertices,
                        pending.Data.OpaqueIndices,
                        pending.Data.CutoutVertices,
                        pending.Data.CutoutIndices,
                        pending.Data.TranslucentVertices,
                        pending.Data.TranslucentIndices);
                    Profiler.EndSample();
                    long tu1 = System.Diagnostics.Stopwatch.GetTimestamp();
                    uploadAccum += (float)((tu1 - tu0) * 1000.0 / freq);

                    pending.Data.Dispose();
                    PipelineStats.IncrMeshCompleted();

                    ManagedChunk chunk = _chunkManager.GetChunk(pending.Coord);

                    if (chunk != null)
                    {
                        if (chunk.NeedsRelightAfterMesh)
                        {
                            chunk.NeedsRelightAfterMesh = false;
                            _chunkManager.SetChunkState(chunk, ChunkState.RelightPending);
                        }
                        else if (chunk.DeferredEdits.Count > 0)
                        {
                            Profiler.BeginSample("MS.DeferredEdits");
                            // Apply deferred edits that arrived while the mesh job was in-flight.
                            // This writes to ChunkData now that the job is complete and no
                            // worker thread is reading it. Also fires block entity events.
                            _chunkManager.ApplyDeferredEdits(chunk);
                            Profiler.EndSample();
                        }
                        else if (chunk.NeedsRemesh)
                        {
                            chunk.NeedsRemesh = false;
                            _chunkManager.SetChunkState(chunk, ChunkState.Generated);
                        }
                        else
                        {
                            _chunkManager.SetChunkState(chunk, ChunkState.Ready);
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
                    pending.FrameAge++;
                    _pendingMeshes[i] = pending;
                    i++;
                }
            }

            PipelineStats.PollMeshUploadMs = uploadAccum;

            // Iterate time = residual (total method time minus all measured sub-parts)
            long tEnd = System.Diagnostics.Stopwatch.GetTimestamp();
            float totalMethodMs = (float)((tEnd - td0) * 1000.0 / freq);
            PipelineStats.PollMeshIterateMs = totalMethodMs
                - PipelineStats.PollMeshDisposalsMs
                - uploadAccum;

            Profiler.EndSample();
        }

        /// <summary>
        /// Schedules greedy mesh jobs for Generated chunks.
        /// When applyFilters is true, LOD0-only filtering is applied and frustum chunks
        /// are prioritized (but off-frustum chunks still mesh if budget remains).
        /// During spawn (applyFilters=false), all Generated chunks are meshed unconditionally.
        /// </summary>
        public void ScheduleJobs(bool applyFilters)
        {
            int pendingCount = _pendingMeshes.Count;

            // Adaptive throttle: reduce scheduling when many jobs are in-flight.
            // This leaves main-thread budget for PollCompleted (job completion + GPU upload).
            // Ramp: 0-16 pending = full budget, 17-31 pending = linear ramp-down, 32+ = minimum 1.
            int effectiveMax;

            if (pendingCount <= _throttleThreshold)
            {
                effectiveMax = _maxMeshesPerFrame;
            }
            else
            {
                effectiveMax = math.max(1, _maxMeshesPerFrame - (pendingCount - _throttleThreshold) / 2);
            }

            int slotsAvailable = effectiveMax - math.min(pendingCount, effectiveMax);

            if (slotsAvailable <= 0)
            {
                return;
            }

            Profiler.BeginSample("MS.ScheduleJobs");
            long freq = System.Diagnostics.Stopwatch.Frequency;

            // ── Fill candidates ──
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int candidateCount = applyFilters ? slotsAvailable * 2 : slotsAvailable;
            _chunkManager.FillChunksToMesh(_meshCandidateCache, candidateCount);
            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            PipelineStats.SchedMeshFillMs = (float)((t1 - t0) * 1000.0 / freq);

            // ── Filter + sort ──
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

            long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
            PipelineStats.SchedMeshFilterMs = (float)((t2 - t1) * 1000.0 / freq);

            // Schedule up to slotsAvailable from the prioritized list
            int scheduleCount = _meshCandidateCache.Count < slotsAvailable
                ? _meshCandidateCache.Count
                : slotsAvailable;

            // ── Alloc + Schedule loop ──
            float allocAccum = 0f;
            float schedAccum = 0f;

            for (int i = 0; i < scheduleCount; i++)
            {
                ManagedChunk chunk = _meshCandidateCache[i];

                _chunkManager.SetChunkState(chunk, ChunkState.Meshing);

                long ta0 = System.Diagnostics.Stopwatch.GetTimestamp();
                GreedyMeshData meshData = new GreedyMeshData(Allocator.TempJob);
                long ta1 = System.Diagnostics.Stopwatch.GetTimestamp();
                allocAccum += (float)((ta1 - ta0) * 1000.0 / freq);

                // Schedule combined border extraction job on worker thread (Burst-compiled)
                long ts0 = System.Diagnostics.Stopwatch.GetTimestamp();
                JobHandle borderDependency = ScheduleBorderExtractionJob(chunk.Coord, meshData);

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
                    ChunkCoord = chunk.Coord,
                    OpaqueVertices = meshData.OpaqueVertices,
                    OpaqueIndices = meshData.OpaqueIndices,
                    CutoutVertices = meshData.CutoutVertices,
                    CutoutIndices = meshData.CutoutIndices,
                    TranslucentVertices = meshData.TranslucentVertices,
                    TranslucentIndices = meshData.TranslucentIndices,
                };

                JobHandle meshHandle = meshJob.Schedule(borderDependency);
                long ts1 = System.Diagnostics.Stopwatch.GetTimestamp();
                schedAccum += (float)((ts1 - ts0) * 1000.0 / freq);

                chunk.ActiveJobHandle = meshHandle;

                _pendingMeshes.Add(new PendingMesh
                {
                    Coord = chunk.Coord,
                    Handle = meshHandle,
                    Data = meshData,
                    FrameAge = 0,
                });
                PipelineStats.IncrMeshScheduled();
            }

            PipelineStats.SchedMeshAllocMs = allocAccum;
            PipelineStats.SchedMeshScheduleMs = schedAccum;

            // ── Flush ──
            long tf0 = System.Diagnostics.Stopwatch.GetTimestamp();

            if (scheduleCount > 0)
            {
                JobHandle.ScheduleBatchedJobs();
            }

            long tf1 = System.Diagnostics.Stopwatch.GetTimestamp();
            PipelineStats.SchedMeshFlushMs = (float)((tf1 - tf0) * 1000.0 / freq);

            Profiler.EndSample();
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

            if (_dummyBorder.IsCreated)
            {
                _dummyBorder.Dispose();
            }
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
        /// Schedules a single Burst-compiled ExtractAllBordersJob that extracts all 6
        /// border slices from neighboring chunks. Replaces the previous 6 separate
        /// ExtractSingleBorderJob schedules to reduce scheduling overhead.
        /// Missing neighbors use _dummyBorder with HasXxx=false to skip extraction.
        /// </summary>
        private JobHandle ScheduleBorderExtractionJob(int3 coord, GreedyMeshData meshData)
        {
            ManagedChunk nPX = GetMeshableNeighbor(coord + new int3(1, 0, 0));
            ManagedChunk nNX = GetMeshableNeighbor(coord + new int3(-1, 0, 0));
            ManagedChunk nPY = GetMeshableNeighbor(coord + new int3(0, 1, 0));
            ManagedChunk nNY = GetMeshableNeighbor(coord + new int3(0, -1, 0));
            ManagedChunk nPZ = GetMeshableNeighbor(coord + new int3(0, 0, 1));
            ManagedChunk nNZ = GetMeshableNeighbor(coord + new int3(0, 0, -1));

            ExtractAllBordersJob job = new ExtractAllBordersJob
            {
                NeighborPosXData = nPX != null ? nPX.Data : _dummyBorder,
                NeighborNegXData = nNX != null ? nNX.Data : _dummyBorder,
                NeighborPosYData = nPY != null ? nPY.Data : _dummyBorder,
                NeighborNegYData = nNY != null ? nNY.Data : _dummyBorder,
                NeighborPosZData = nPZ != null ? nPZ.Data : _dummyBorder,
                NeighborNegZData = nNZ != null ? nNZ.Data : _dummyBorder,

                HasPosX = nPX != null,
                HasNegX = nNX != null,
                HasPosY = nPY != null,
                HasNegY = nNY != null,
                HasPosZ = nPZ != null,
                HasNegZ = nNZ != null,

                OutputPosX = meshData.NeighborPosX,
                OutputNegX = meshData.NeighborNegX,
                OutputPosY = meshData.NeighborPosY,
                OutputNegY = meshData.NeighborNegY,
                OutputPosZ = meshData.NeighborPosZ,
                OutputNegZ = meshData.NeighborNegZ,
            };

            return job.Schedule();
        }

        private ManagedChunk GetMeshableNeighbor(int3 coord)
        {
            ManagedChunk c = _chunkManager.GetChunk(coord);

            if (c == null || c.State < ChunkState.RelightPending || !c.Data.IsCreated)
            {
                return null;
            }

            return c;
        }

        private struct PendingMesh
        {
            public int3 Coord;
            public JobHandle Handle;
            public GreedyMeshData Data;
            public int FrameAge;
        }
    }
}
