using System.Collections.Generic;
using System.Diagnostics;

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
    ///     Owns the mesh job queue: scheduling greedy mesh jobs for LOD0 chunks,
    ///     polling for completion, uploading to ChunkMeshStore, and disposing resources.
    /// </summary>
    public sealed class MeshScheduler
    {
        private readonly ChunkManager _chunkManager;
        private readonly ChunkMeshStore _chunkMeshStore;

        /// <summary>Wall-clock millisecond budget for completion polling each frame.</summary>
        private readonly float _completionBudgetMs;
        private readonly ChunkCulling _culling;

        /// <summary>
        ///     Reusable list for FillChunksToMesh — avoids per-frame allocation.
        ///     Owner: MeshScheduler. Lifetime: application.
        /// </summary>
        private readonly List<ManagedChunk> _meshCandidateCache = new();
        private readonly NativeAtlasLookup _nativeAtlasLookup;
        private readonly NativeStateRegistry _nativeStateRegistry;

        /// <summary>
        ///     Mesh jobs whose chunk was unloaded while in-flight. Polled and disposed
        ///     at most 2 per frame to spread out the disposal cost.
        /// </summary>
        private readonly List<PendingMesh> _pendingDisposals = new();
        /// <summary>Mesh jobs currently running on worker threads.</summary>
        private readonly List<PendingMesh> _pendingMeshes = new();
        private readonly IPipelineStats _pipelineStats;

        /// <summary>
        ///     Dummy NativeArray passed to ExtractAllBordersJob for missing neighbors.
        ///     The job's HasXxx flags prevent reading from it; this satisfies the safety system.
        ///     Owner: MeshScheduler. Lifetime: application. Allocator: Persistent.
        /// </summary>
        private NativeArray<StateId> _dummyBorder;

        /// <summary>
        ///     Dummy NativeArray passed to GreedyMeshJob.LiquidData for chunks without liquid.
        ///     HasLiquidData=false prevents the job from reading it; this satisfies the safety system.
        ///     Owner: MeshScheduler. Lifetime: application. Allocator: Persistent.
        /// </summary>
        private NativeArray<byte> _dummyLiquid;
        private int _maxMeshCompletionsPerFrame;
        private int _maxMeshesPerFrame;

        /// <summary>
        ///     In-flight job count above which the scheduler linearly ramps down new schedules.
        ///     Derived from render distance via SchedulingConfig.ThrottleThreshold.
        /// </summary>
        private int _throttleThreshold = 16;

        public MeshScheduler(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            NativeAtlasLookup nativeAtlasLookup,
            ChunkMeshStore chunkMeshStore,
            ChunkCulling culling,
            IPipelineStats pipelineStats,
            int maxMeshesPerFrame,
            int maxMeshCompletionsPerFrame,
            float completionBudgetMs)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
            _nativeAtlasLookup = nativeAtlasLookup;
            _chunkMeshStore = chunkMeshStore;
            _culling = culling;
            _pipelineStats = pipelineStats;
            _maxMeshesPerFrame = maxMeshesPerFrame;
            _maxMeshCompletionsPerFrame = maxMeshCompletionsPerFrame;
            _completionBudgetMs = completionBudgetMs;
            _dummyBorder = new NativeArray<StateId>(1, Allocator.Persistent);
            _dummyLiquid = new NativeArray<byte>(1, Allocator.Persistent);
        }

        /// <summary>Number of mesh jobs currently in-flight on worker threads.</summary>
        public int PendingCount
        {
            get { return _pendingMeshes.Count; }
        }

        /// <summary>
        ///     Re-derives scheduling and throttle limits from the current render distance.
        ///     Called when the player changes render distance at runtime.
        /// </summary>
        /// <param name="renderDistance">New render distance in chunks.</param>
        public void UpdateConfig(int renderDistance)
        {
            _maxMeshesPerFrame = SchedulingConfig.MaxMeshesPerFrame(renderDistance);
            _maxMeshCompletionsPerFrame = SchedulingConfig.MaxMeshCompletionsPerFrame(renderDistance);
            _throttleThreshold = SchedulingConfig.ThrottleThreshold(renderDistance);
        }

        /// <summary>
        ///     Polls in-flight mesh jobs for completion, uploads finished mesh data to
        ///     ChunkMeshStore, handles post-mesh state transitions (relight, deferred edits,
        ///     remesh), and processes deferred disposals. Time-budgeted to avoid frame spikes.
        /// </summary>
        public void PollCompleted()
        {
            Profiler.BeginSample("MS.PollCompleted");

            long freq = Stopwatch.Frequency;

            long td0 = Stopwatch.GetTimestamp();
            PollPendingDisposals();
            long td1 = Stopwatch.GetTimestamp();
            _pipelineStats.PollMeshDisposalsMs = (float)((td1 - td0) * 1000.0 / freq);

            int completedThisFrame = 0;
            float uploadAccum = 0f;
            bool firstCheck = true;

            // First pass: process urgent (player-edit) completions regardless of budget.
            // Mirrors Luanti's m_queue_out_urgent being drained first.
            for (int u = 0; u < _pendingMeshes.Count;)
            {
                PendingMesh pending = _pendingMeshes[u];

                if (!pending.IsUrgent || pending.FrameAge < 1)
                {
                    if (pending.IsUrgent)
                    {
                        pending.FrameAge++;
                        _pendingMeshes[u] = pending;
                    }

                    u++;
                    continue;
                }

                bool urgentForceComplete = pending.FrameAge > 300;

                if (!pending.Handle.IsCompleted && !urgentForceComplete)
                {
                    pending.FrameAge++;
                    _pendingMeshes[u] = pending;
                    u++;
                    continue;
                }

                if (urgentForceComplete)
                {
                    UnityEngine.Debug.LogWarning(
                        "[MeshScheduler] Force-completing stale urgent mesh job for chunk " +
                        $"{pending.Coord} after {pending.FrameAge} frames");
                }

                CompleteMesh(pending, ref completedThisFrame, ref uploadAccum, freq);

                // Swap-back removal
                int last = _pendingMeshes.Count - 1;

                if (u < last)
                {
                    _pendingMeshes[u] = _pendingMeshes[last];
                }

                _pendingMeshes.RemoveAt(last);
            }

            // Second pass: process remaining (non-urgent) completions within budget.
            FrameBudget budget = new(_completionBudgetMs);
            int i = 0;

            while (i < _pendingMeshes.Count)
            {
                if (completedThisFrame >= _maxMeshCompletionsPerFrame || budget.IsExhausted())
                {
                    break;
                }

                PendingMesh pending = _pendingMeshes[i];

                // Skip urgent items — already handled by the first pass.
                // Prevents double FrameAge increment.
                if (pending.IsUrgent)
                {
                    i++;
                    continue;
                }

                bool forceComplete = false;

                if (pending.FrameAge > 300)
                {
                    UnityEngine.Debug.LogWarning(
                        "[MeshScheduler] Force-completing stale mesh job for chunk " +
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
                    long tf0 = Stopwatch.GetTimestamp();
                    isCompleted = pending.Handle.IsCompleted;
                    long tf1 = Stopwatch.GetTimestamp();
                    _pipelineStats.PollMeshFirstIsCompletedMs = (float)((tf1 - tf0) * 1000.0 / freq);
                    firstCheck = false;
                }
                else
                {
                    isCompleted = pending.Handle.IsCompleted;
                }

                if (isCompleted)
                {
                    CompleteMesh(pending, ref completedThisFrame, ref uploadAccum, freq);

                    // Swap-back: O(1) removal instead of O(n) RemoveAt shift
                    int last = _pendingMeshes.Count - 1;

                    if (i < last)
                    {
                        _pendingMeshes[i] = _pendingMeshes[last];
                    }

                    _pendingMeshes.RemoveAt(last);
                }
                else
                {
                    pending.FrameAge++;
                    _pendingMeshes[i] = pending;
                    i++;
                }
            }

            _pipelineStats.PollMeshUploadMs = uploadAccum;

            // Iterate time = residual (total method time minus all measured sub-parts)
            long tEnd = Stopwatch.GetTimestamp();
            float totalMethodMs = (float)((tEnd - td0) * 1000.0 / freq);
            _pipelineStats.PollMeshIterateMs = totalMethodMs
                                               - _pipelineStats.PollMeshDisposalsMs
                                               - uploadAccum;

            Profiler.EndSample();
        }

        /// <summary>
        ///     Completes a single mesh job: calls Handle.Complete(), uploads to GPU,
        ///     disposes native data, and handles post-mesh state transitions.
        ///     Shared between the urgent-first pass and the normal budgeted pass.
        /// </summary>
        private void CompleteMesh(
            PendingMesh pending,
            ref int completedThisFrame,
            ref float uploadAccum,
            long freq)
        {
            completedThisFrame++;
            long tc0 = Stopwatch.GetTimestamp();
            pending.Handle.Complete();
            long tc1 = Stopwatch.GetTimestamp();
            float completeMs = (float)((tc1 - tc0) * 1000.0 / freq);
            _pipelineStats.RecordMeshComplete(completeMs);

            long tu0 = Stopwatch.GetTimestamp();
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
            long tu1 = Stopwatch.GetTimestamp();
            uploadAccum += (float)((tu1 - tu0) * 1000.0 / freq);

            pending.Data.Dispose();
            _pipelineStats.IncrMeshCompleted();

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
                    // Force-complete any in-flight mesh jobs whose border extraction
                    // reads this chunk's Data as a neighbor, before we write to it.
                    ForceCompleteNeighborDeps(chunk.Coord);
                    Profiler.BeginSample("MS.DeferredEdits");
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
                    chunk.HasPlayerEdit = false;
                }

                chunk.ActiveJobHandle = default;
                chunk.RenderedLODLevel = 0;
            }
        }

        /// <summary>
        ///     Schedules greedy mesh jobs for Generated chunks.
        ///     When applyFilters is true, LOD0-only filtering is applied and frustum chunks
        ///     are prioritized (but off-frustum chunks still mesh if budget remains).
        ///     During spawn (applyFilters=false), all Generated chunks are meshed unconditionally.
        /// </summary>
        public void ScheduleJobs(bool applyFilters, int3 cameraChunkCoord, float3 cameraForwardXZ)
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
            long freq = Stopwatch.Frequency;

            // ── Fill candidates ──
            long t0 = Stopwatch.GetTimestamp();
            int candidateCount = applyFilters ? slotsAvailable * 2 : slotsAvailable;
            _chunkManager.FillChunksToMesh(
                _meshCandidateCache, candidateCount, cameraChunkCoord, cameraForwardXZ);
            long t1 = Stopwatch.GetTimestamp();
            _pipelineStats.SchedMeshFillMs = (float)((t1 - t0) * 1000.0 / freq);

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

            long t2 = Stopwatch.GetTimestamp();
            _pipelineStats.SchedMeshFilterMs = (float)((t2 - t1) * 1000.0 / freq);

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

                // Guard: skip chunks whose data was disposed between candidate
                // collection and scheduling (e.g. liquid cross-boundary edits
                // triggering DirtyNeighborBorders → unload race).
                if (!chunk.LightData.IsCreated || !chunk.Data.IsCreated)
                {
                    continue;
                }

                // Fast-track all-air chunks to Ready without scheduling a mesh job.
                // Greedy meshing would produce zero vertices anyway.
                if (chunk.IsAllAir)
                {
                    _chunkManager.SetChunkState(chunk, ChunkState.Ready);
                    chunk.RenderedLODLevel = 0;
                    continue;
                }

                _chunkManager.SetChunkState(chunk, ChunkState.Meshing);

                long ta0 = Stopwatch.GetTimestamp();
                GreedyMeshData meshData = new(Allocator.TempJob);
                long ta1 = Stopwatch.GetTimestamp();
                allocAccum += (float)((ta1 - ta0) * 1000.0 / freq);

                // Extract liquid neighbor ghost slabs BEFORE scheduling any jobs.
                // This may call neighbor.LiquidJobHandle.Complete() on the main thread,
                // which can trigger work-stealing that completes other in-flight jobs
                // and potentially invalidates this chunk's NativeArrays.
                long ts0 = Stopwatch.GetTimestamp();
                ExtractLiquidBorderSlabs(chunk, meshData);

                // Re-check: completing neighbor liquid jobs may have triggered state
                // changes or disposals that invalidated this chunk's data.
                if (!chunk.LightData.IsCreated || !chunk.Data.IsCreated)
                {
                    meshData.Dispose();
                    _chunkManager.SetChunkState(chunk, ChunkState.Generated);

                    continue;
                }

                // Schedule combined border extraction job on worker thread (Burst-compiled)
                JobHandle borderDependency = ScheduleBorderExtractionJob(chunk.Coord, meshData);

                GreedyMeshJob meshJob = new()
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
                    LiquidData = chunk.LiquidData.IsCreated ? chunk.LiquidData : _dummyLiquid,
                    HasLiquidData = chunk.LiquidData.IsCreated,
                    LiquidNeighborPosX = meshData.LiquidNeighborPosX,
                    LiquidNeighborNegX = meshData.LiquidNeighborNegX,
                    LiquidNeighborPosZ = meshData.LiquidNeighborPosZ,
                    LiquidNeighborNegZ = meshData.LiquidNeighborNegZ,
                    ChunkCoord = chunk.Coord,
                    OpaqueVertices = meshData.OpaqueVertices,
                    OpaqueIndices = meshData.OpaqueIndices,
                    CutoutVertices = meshData.CutoutVertices,
                    CutoutIndices = meshData.CutoutIndices,
                    TranslucentVertices = meshData.TranslucentVertices,
                    TranslucentIndices = meshData.TranslucentIndices,
                };

                // Depend on both border extraction and any in-flight liquid job
                // writing to this chunk's LiquidData. CombineDependencies with a
                // default handle (no liquid job) is a no-op.
                JobHandle meshDep = JobHandle.CombineDependencies(
                    borderDependency, chunk.LiquidJobHandle);
                JobHandle meshHandle = meshJob.Schedule(meshDep);
                long ts1 = Stopwatch.GetTimestamp();
                schedAccum += (float)((ts1 - ts0) * 1000.0 / freq);

                chunk.ActiveJobHandle = meshHandle;

                _pendingMeshes.Add(new PendingMesh
                {
                    Coord = chunk.Coord,
                    Handle = meshHandle,
                    Data = meshData,
                    FrameAge = 0,
                    IsUrgent = chunk.HasPlayerEdit,
                });
                _pipelineStats.IncrMeshScheduled();
            }

            _pipelineStats.SchedMeshAllocMs = allocAccum;
            _pipelineStats.SchedMeshScheduleMs = schedAccum;

            // ── Flush ──
            long tf0 = Stopwatch.GetTimestamp();

            if (scheduleCount > 0)
            {
                JobHandle.ScheduleBatchedJobs();
            }

            long tf1 = Stopwatch.GetTimestamp();
            _pipelineStats.SchedMeshFlushMs = (float)((tf1 - tf0) * 1000.0 / freq);

            Profiler.EndSample();
        }

        /// <summary>
        ///     Moves any in-flight mesh job for the given coord to the deferred disposal queue.
        ///     Called when a chunk is being unloaded so its slot stops consuming completion budget.
        /// </summary>
        /// <param name="coord">Chunk coordinate being unloaded.</param>
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
        ///     Force-completes any pending mesh or disposal jobs whose border extraction
        ///     reads the given coord's chunk data. Must be called before the chunk's
        ///     NativeArray is returned to the pool so that Unity's job safety locks are
        ///     released. The jobs remain in their lists for normal processing.
        ///     Checks IsCompleted first to avoid work-stealing on unrelated jobs.
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

        /// <summary>
        ///     Force-completes all in-flight mesh and disposal jobs, then disposes their
        ///     NativeContainers and the dummy border array. Called during application shutdown.
        /// </summary>
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

            if (_dummyLiquid.IsCreated)
            {
                _dummyLiquid.Dispose();
            }
        }

        /// <summary>
        ///     Disposes completed mesh jobs that were deferred by CleanupCoord.
        ///     Budgeted: disposes at most 2 items per frame to avoid burst spikes
        ///     when many chunks are unloaded simultaneously.
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
        ///     Schedules a single Burst-compiled ExtractAllBordersJob that extracts all 6
        ///     border slices from neighboring chunks using cached Neighbors[] references.
        ///     Missing neighbors use _dummyBorder with HasXxx=false to skip extraction.
        /// </summary>
        private JobHandle ScheduleBorderExtractionJob(int3 coord, GreedyMeshData meshData)
        {
            ManagedChunk chunk = _chunkManager.GetChunk(coord);

            ManagedChunk nPX = ValidateMeshableNeighbor(chunk != null ? chunk.Neighbors[0] : null);
            ManagedChunk nNX = ValidateMeshableNeighbor(chunk != null ? chunk.Neighbors[1] : null);
            ManagedChunk nPY = ValidateMeshableNeighbor(chunk != null ? chunk.Neighbors[2] : null);
            ManagedChunk nNY = ValidateMeshableNeighbor(chunk != null ? chunk.Neighbors[3] : null);
            ManagedChunk nPZ = ValidateMeshableNeighbor(chunk != null ? chunk.Neighbors[4] : null);
            ManagedChunk nNZ = ValidateMeshableNeighbor(chunk != null ? chunk.Neighbors[5] : null);

            // Secondary guard: re-verify Data.IsCreated right before capturing
            // the NativeArray struct. ExtractLiquidBorderSlabs (called before this
            // method) may trigger work-stealing via LiquidJobHandle.Complete() that
            // indirectly invalidates a neighbor's NativeArray between the
            // ValidateMeshableNeighbor check and job scheduling.
            bool hasPX = nPX != null && nPX.Data.IsCreated;
            bool hasNX = nNX != null && nNX.Data.IsCreated;
            bool hasPY = nPY != null && nPY.Data.IsCreated;
            bool hasNY = nNY != null && nNY.Data.IsCreated;
            bool hasPZ = nPZ != null && nPZ.Data.IsCreated;
            bool hasNZ = nNZ != null && nNZ.Data.IsCreated;

            NativeArray<StateId> pxData = hasPX ? nPX.Data : _dummyBorder;
            NativeArray<StateId> nxData = hasNX ? nNX.Data : _dummyBorder;
            NativeArray<StateId> pyData = hasPY ? nPY.Data : _dummyBorder;
            NativeArray<StateId> nyData = hasNY ? nNY.Data : _dummyBorder;
            NativeArray<StateId> pzData = hasPZ ? nPZ.Data : _dummyBorder;
            NativeArray<StateId> nzData = hasNZ ? nNZ.Data : _dummyBorder;

            ExtractAllBordersJob job = new()
            {
                NeighborPosXData = pxData,
                NeighborNegXData = nxData,
                NeighborPosYData = pyData,
                NeighborNegYData = nyData,
                NeighborPosZData = pzData,
                NeighborNegZData = nzData,
                HasPosX = hasPX,
                HasNegX = hasNX,
                HasPosY = hasPY,
                HasNegY = hasNY,
                HasPosZ = hasPZ,
                HasNegZ = hasNZ,
                OutputPosX = meshData.NeighborPosX,
                OutputNegX = meshData.NeighborNegX,
                OutputPosY = meshData.NeighborPosY,
                OutputNegY = meshData.NeighborNegY,
                OutputPosZ = meshData.NeighborPosZ,
                OutputNegZ = meshData.NeighborNegZ,
            };

            return job.Schedule();
        }

        /// <summary>
        ///     Validates a cached neighbor reference for border extraction.
        ///     Returns null if the neighbor is missing, not yet generated, or data not valid.
        /// </summary>
        private static ManagedChunk ValidateMeshableNeighbor(ManagedChunk candidate)
        {
            if (candidate == null || candidate.State < ChunkState.RelightPending || !candidate.Data.IsCreated)
            {
                return null;
            }

            return candidate;
        }

        /// <summary>
        ///     Extracts liquid border slabs from the 4 horizontal neighbors (+X, -X, +Z, -Z)
        ///     into the GreedyMeshData's LiquidNeighbor arrays for cross-chunk corner level
        ///     interpolation. Completes any in-flight liquid jobs on neighbors before reading.
        /// </summary>
        private void ExtractLiquidBorderSlabs(ManagedChunk centerChunk, GreedyMeshData meshData)
        {
            ExtractLiquidBorderSlab(centerChunk, 0, meshData.LiquidNeighborPosX);
            ExtractLiquidBorderSlab(centerChunk, 1, meshData.LiquidNeighborNegX);
            ExtractLiquidBorderSlab(centerChunk, 4, meshData.LiquidNeighborPosZ);
            ExtractLiquidBorderSlab(centerChunk, 5, meshData.LiquidNeighborNegZ);
        }

        /// <summary>
        ///     Extracts a single liquid border slab from a neighbor chunk.
        ///     face: 0=+X, 1=-X, 4=+Z, 5=-Z (matching GetFaceNormal convention).
        ///     The output slab is indexed [y * Size + edgeCoord] and contains raw
        ///     LiquidCell bytes from the neighbor's boundary slice.
        /// </summary>
        private void ExtractLiquidBorderSlab(ManagedChunk centerChunk, int face, NativeArray<byte> output)
        {
            int3 offset;

            switch (face)
            {
                case 0:
                    offset = new int3(1, 0, 0);
                    break;
                case 1:
                    offset = new int3(-1, 0, 0);
                    break;
                case 4:
                    offset = new int3(0, 0, 1);
                    break;
                default:
                    offset = new int3(0, 0, -1);
                    break;
            }

            int neighborIndex = face switch
            {
                0 => 0,
                1 => 1,
                4 => 4,
                _ => 5,
            };

            ManagedChunk neighbor = centerChunk.Neighbors[neighborIndex];

            if (neighbor == null || !neighbor.LiquidData.IsCreated)
            {
                // output is already zero-initialized by GreedyMeshData constructor
                return;
            }

            // Complete any in-flight liquid job before reading neighbor's LiquidData
            neighbor.LiquidJobHandle.Complete();

            NativeArray<byte> srcLiquid = neighbor.LiquidData;

            for (int y = 0; y < ChunkConstants.Size; y++)
            {
                for (int i = 0; i < ChunkConstants.Size; i++)
                {
                    int srcIndex;

                    switch (face)
                    {
                        case 0: // +X neighbor: read x=0 slice, indexed [y*Size+z]
                            srcIndex = y * ChunkConstants.SizeSquared + i * ChunkConstants.Size + 0;
                            break;
                        case 1: // -X neighbor: read x=Size-1 slice, indexed [y*Size+z]
                            srcIndex = y * ChunkConstants.SizeSquared + i * ChunkConstants.Size + (ChunkConstants.Size - 1);
                            break;
                        case 4: // +Z neighbor: read z=0 slice, indexed [y*Size+x]
                            srcIndex = y * ChunkConstants.SizeSquared + 0 * ChunkConstants.Size + i;
                            break;
                        default: // -Z neighbor: read z=Size-1 slice, indexed [y*Size+x]
                            srcIndex = y * ChunkConstants.SizeSquared + (ChunkConstants.Size - 1) * ChunkConstants.Size + i;
                            break;
                    }

                    output[y * ChunkConstants.Size + i] = srcLiquid[srcIndex];
                }
            }
        }

        private struct PendingMesh
        {
            public int3 Coord;
            public JobHandle Handle;
            public GreedyMeshData Data;
            public int FrameAge;
            public bool IsUrgent;
        }
    }
}
