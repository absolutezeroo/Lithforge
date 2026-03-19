using System;
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
    ///     Owns the LOD mesh job queue: assigning LOD levels based on camera distance,
    ///     scheduling downsample + mesh jobs, polling for completion, and disposing resources.
    ///     Handles both first-time LOD meshing (Generated chunks) and LOD transitions (Ready chunks).
    /// </summary>
    public sealed class LODScheduler
    {
        /// <summary>Chunk manager for state transitions and chunk queries.</summary>
        private readonly ChunkManager _chunkManager;

        /// <summary>GPU mesh store for uploading completed LOD meshes.</summary>
        private readonly ChunkMeshStore _chunkMeshStore;

        /// <summary>Wall-clock millisecond budget for completion polling each frame.</summary>
        private readonly float _completionBudgetMs;

        /// <summary>Frustum culling helper for LOD transition prioritization.</summary>
        private readonly ChunkCulling _culling;

        /// <summary>Reusable cache for generated chunks during LOD level assignment.</summary>
        private readonly List<ManagedChunk> _generatedChunksCache = new();

        /// <summary>Reusable cache for generated chunks with LOD level greater than zero.</summary>
        private readonly List<ManagedChunk> _generatedLODCache = new();

        /// <summary>Burst-accessible atlas lookup for texture indices during LOD meshing.</summary>
        private readonly NativeAtlasLookup _nativeAtlasLookup;

        /// <summary>Burst-accessible state registry for block state lookups during LOD meshing.</summary>
        private readonly NativeStateRegistry _nativeStateRegistry;

        /// <summary>LOD mesh jobs whose chunks were unloaded while in-flight, awaiting deferred disposal.</summary>
        private readonly List<PendingLODMesh> _pendingLODDisposals = new();

        /// <summary>LOD mesh jobs currently running on worker threads.</summary>
        private readonly List<PendingLODMesh> _pendingLODMeshes = new();

        /// <summary>Pipeline statistics tracker for LOD scheduled/completed counters.</summary>
        private readonly IPipelineStats _pipelineStats;

        /// <summary>Reusable cache for Ready chunks during LOD level assignment. Avoids per-frame allocation.</summary>
        private readonly List<ManagedChunk> _readyChunksCache = new();

        /// <summary>Chebyshev XZ chunk distance at which LOD1 (2x2x2 merge) begins.</summary>
        private int _lod1Distance;

        /// <summary>Chebyshev XZ chunk distance at which LOD2 (4x4x4 merge) begins.</summary>
        private int _lod2Distance;

        /// <summary>Chebyshev XZ chunk distance at which LOD3 (8x8x8 merge) begins.</summary>
        private int _lod3Distance;

        /// <summary>Maximum number of LOD mesh completions to process per frame.</summary>
        private int _maxLODCompletionsPerFrame;

        /// <summary>Maximum number of LOD mesh jobs to schedule per frame.</summary>
        private int _maxLODMeshesPerFrame;

        /// <summary>Creates a new LOD scheduler with the given dependencies and configuration.</summary>
        public LODScheduler(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            NativeAtlasLookup nativeAtlasLookup,
            ChunkMeshStore chunkMeshStore,
            ChunkCulling culling,
            IPipelineStats pipelineStats,
            int maxLODMeshesPerFrame,
            int maxLODCompletionsPerFrame,
            float completionBudgetMs,
            int lod1Distance,
            int lod2Distance,
            int lod3Distance)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
            _nativeAtlasLookup = nativeAtlasLookup;
            _chunkMeshStore = chunkMeshStore;
            _culling = culling;
            _pipelineStats = pipelineStats;
            _maxLODMeshesPerFrame = maxLODMeshesPerFrame;
            _maxLODCompletionsPerFrame = maxLODCompletionsPerFrame;
            _completionBudgetMs = completionBudgetMs;
            _lod1Distance = lod1Distance;
            _lod2Distance = lod2Distance;
            _lod3Distance = lod3Distance;
        }

        /// <summary>Number of LOD mesh jobs currently in-flight on worker threads.</summary>
        public int PendingCount
        {
            get { return _pendingLODMeshes.Count; }
        }

        /// <summary>Re-derives LOD scheduling limits and distances from the current render distance.</summary>
        public void UpdateConfig(int renderDistance)
        {
            _maxLODMeshesPerFrame = SchedulingConfig.MaxLODMeshesPerFrame(renderDistance);
            _maxLODCompletionsPerFrame = SchedulingConfig.MaxLODCompletionsPerFrame(renderDistance);
            _lod1Distance = SchedulingConfig.LOD1Distance(renderDistance);
            _lod2Distance = SchedulingConfig.LOD2Distance(renderDistance);
            _lod3Distance = SchedulingConfig.LOD3Distance(renderDistance);
        }

        /// <summary>
        ///     Polls in-flight LOD mesh jobs for completion, uploads finished meshes to
        ///     the GPU, and transitions chunk states. Time-budgeted to avoid frame spikes.
        /// </summary>
        public void PollCompleted()
        {
            Profiler.BeginSample("LOD.PollCompleted");

            PollPendingLODDisposals();

            FrameBudget budget = new(_completionBudgetMs);
            int completedThisFrame = 0;

            for (int i = _pendingLODMeshes.Count - 1; i >= 0; i--)
            {
                if (completedThisFrame >= _maxLODCompletionsPerFrame || budget.IsExhausted())
                {
                    break;
                }

                PendingLODMesh pending = _pendingLODMeshes[i];

                if (pending.Handle.IsCompleted)
                {
                    pending.Handle.Complete();
                    completedThisFrame++;

                    // Upload as single-submesh opaque mesh
                    _chunkMeshStore.UpdateRendererSingleMesh(
                        pending.Coord,
                        pending.Data.Vertices,
                        pending.Data.Indices);

                    pending.Data.Dispose();
                    _pipelineStats.IncrLODCompleted();

                    ManagedChunk chunk = _chunkManager.GetChunk(pending.Coord);

                    if (chunk != null)
                    {
                        // Transition Generated->Ready for first-time LOD mesh
                        if (chunk.State == ChunkState.Meshing)
                        {
                            if (chunk.NeedsRemesh)
                            {
                                chunk.NeedsRemesh = false;
                                _chunkManager.SetChunkState(chunk, ChunkState.Generated);
                            }
                            else
                            {
                                _chunkManager.SetChunkState(chunk, ChunkState.Ready);
                            }

                            chunk.ActiveJobHandle = default;
                        }

                        chunk.RenderedLODLevel = pending.LODLevel;
                    }

                    _pendingLODMeshes.RemoveAt(i);
                }
            }

            Profiler.EndSample();
        }

        /// <summary>
        ///     Assigns LOD levels to both Ready and Generated chunks based on camera distance.
        ///     Must be called before MeshScheduler.ScheduleJobs and LODScheduler.ScheduleJobs
        ///     so that chunks get their LOD level before the schedulers decide who to mesh.
        /// </summary>
        /// <summary>
        ///     Assigns LOD levels to Ready and Generated chunks using minimum distance
        ///     over all player origins. A chunk within LOD0 range of any player gets LOD0.
        /// </summary>
        public void UpdateLODLevels(ReadOnlySpan<int3> playerChunkCoords)
        {
            // Always update Ready chunks every frame (LOD transitions on meshed chunks)
            _chunkManager.FillReadyChunks(_readyChunksCache);
            AssignLODLevels(_readyChunksCache, playerChunkCoords);

            _chunkManager.FillGeneratedChunks(_generatedChunksCache);
            AssignLODLevels(_generatedChunksCache, playerChunkCoords);
        }

        /// <summary>
        ///     Single-player backward-compatible wrapper for <see cref="UpdateLODLevels(ReadOnlySpan{int3})" />.
        /// </summary>
        public void UpdateLODLevels(int3 cameraChunkCoord)
        {
            Span<int3> single = stackalloc int3[1];
            single[0] = cameraChunkCoord;
            UpdateLODLevels((ReadOnlySpan<int3>)single);
        }

        /// <summary>
        ///     Schedules LOD mesh jobs from two sources:
        ///     1. Generated chunks with LODLevel > 0 (first-time LOD mesh)
        ///     2. Ready chunks with changed LOD level (LOD transition)
        ///     MUST be called after UpdateLODLevels() in the same frame,
        ///     as it reuses _readyChunksCache populated there.
        /// </summary>
        public void ScheduleJobs()
        {
            int slotsAvailable = _maxLODMeshesPerFrame - _pendingLODMeshes.Count;

            if (slotsAvailable <= 0)
            {
                return;
            }

            Profiler.BeginSample("LOD.Schedule");

            int scheduled = 0;

            // Source 1: Generated chunks with LODLevel > 0 (first-time LOD mesh)
            // These are chunks that were never meshed, or were invalidated back to Generated
            // while they had a non-zero LOD level.
            _chunkManager.FillGeneratedChunksWithLOD(_generatedLODCache);

            for (int i = 0; i < _generatedLODCache.Count && scheduled < slotsAvailable; i++)
            {
                ManagedChunk chunk = _generatedLODCache[i];

                if (IsLODPending(chunk.Coord))
                {
                    continue;
                }

                ScheduleLODMesh(chunk);
                _chunkManager.SetChunkState(chunk, ChunkState.Meshing);
                scheduled++;
            }

            // Source 2: Ready chunks with changed LOD level (LOD transition)
            // _readyChunksCache was already populated by UpdateLODLevels()
            for (int i = 0; i < _readyChunksCache.Count && scheduled < slotsAvailable; i++)
            {
                ManagedChunk chunk = _readyChunksCache[i];

                if (chunk.LODLevel == 0 || chunk.LODLevel == chunk.RenderedLODLevel)
                {
                    continue;
                }

                if (IsLODPending(chunk.Coord))
                {
                    continue;
                }

                if (!_culling.IsInFrustum(chunk.Coord))
                {
                    continue;
                }

                ScheduleLODMesh(chunk);
                scheduled++;
            }

            if (scheduled > 0)
            {
                JobHandle.ScheduleBatchedJobs();
            }

            Profiler.EndSample();
        }

        /// <summary>Moves any in-flight LOD mesh job for the given coord to the deferred disposal queue.</summary>
        public void CleanupCoord(int3 coord)
        {
            for (int i = _pendingLODMeshes.Count - 1; i >= 0; i--)
            {
                if (_pendingLODMeshes[i].Coord.Equals(coord))
                {
                    _pendingLODDisposals.Add(_pendingLODMeshes[i]);
                    _pendingLODMeshes.RemoveAt(i);
                }
            }
        }

        /// <summary>Force-completes all in-flight and deferred LOD mesh jobs and disposes their data.</summary>
        public void Shutdown()
        {
            for (int i = 0; i < _pendingLODMeshes.Count; i++)
            {
                _pendingLODMeshes[i].Handle.Complete();
                _pendingLODMeshes[i].Data.Dispose();
            }

            _pendingLODMeshes.Clear();

            for (int i = 0; i < _pendingLODDisposals.Count; i++)
            {
                _pendingLODDisposals[i].Handle.Complete();
                _pendingLODDisposals[i].Data.Dispose();
            }

            _pendingLODDisposals.Clear();
        }

        /// <summary>
        ///     Drains the lazy disposal queue for LOD mesh jobs. Jobs that were in-flight
        ///     when their chunk was unloaded are polled here — once complete, their TempJob
        ///     data is disposed. Called at the start of PollCompleted each frame.
        /// </summary>
        private void PollPendingLODDisposals()
        {
            for (int i = _pendingLODDisposals.Count - 1; i >= 0; i--)
            {
                if (_pendingLODDisposals[i].Handle.IsCompleted)
                {
                    _pendingLODDisposals[i].Handle.Complete();
                    _pendingLODDisposals[i].Data.Dispose();
                    _pendingLODDisposals.RemoveAt(i);
                }
            }
        }

        /// <summary>Assigns LOD levels to the given chunks based on Chebyshev XZ distance from the camera chunk.</summary>
        /// <summary>
        ///     Assigns LOD levels to chunks using the minimum distance over all player origins.
        ///     A chunk near any player gets the finest LOD level. Early-exits once LOD0 is found.
        /// </summary>
        private void AssignLODLevels(List<ManagedChunk> chunks, ReadOnlySpan<int3> playerChunkCoords)
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                ManagedChunk chunk = chunks[i];

                // Pick minimum (finest) LOD over all player origins
                int desiredLOD = 3;

                for (int p = 0; p < playerChunkCoords.Length; p++)
                {
                    int3 diff = chunk.Coord - playerChunkCoords[p];
                    int xzDist = math.max(math.abs(diff.x), math.abs(diff.z));

                    int lodForPlayer;

                    if (xzDist >= _lod3Distance)
                    {
                        lodForPlayer = 3;
                    }
                    else if (xzDist >= _lod2Distance)
                    {
                        lodForPlayer = 2;
                    }
                    else if (xzDist >= _lod1Distance)
                    {
                        lodForPlayer = 1;
                    }
                    else
                    {
                        lodForPlayer = 0;
                    }

                    if (lodForPlayer < desiredLOD)
                    {
                        desiredLOD = lodForPlayer;

                        if (desiredLOD == 0)
                        {
                            break;
                        }
                    }
                }

                if (chunk.LODLevel != desiredLOD)
                {
                    chunk.LODLevel = desiredLOD;

                    // If transitioning to LOD0 and chunk is Ready, trigger full remesh
                    if (desiredLOD == 0 && chunk.State == ChunkState.Ready)
                    {
                        _chunkManager.SetChunkState(chunk, ChunkState.Generated);
                    }
                }
            }
        }

        /// <summary>Returns true if a LOD mesh job is already in-flight for the given coordinate.</summary>
        private bool IsLODPending(int3 coord)
        {
            for (int i = 0; i < _pendingLODMeshes.Count; i++)
            {
                if (_pendingLODMeshes[i].Coord.Equals(coord))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Schedules a VoxelDownsampleJob followed by a LODGreedyMeshJob for the given chunk.</summary>
        private void ScheduleLODMesh(ManagedChunk chunk)
        {
            int lodLevel = chunk.LODLevel;
            int scale = 1 << lodLevel; // 2, 4, or 8
            int gridSize = ChunkConstants.Size / scale;
            int gridVolume = gridSize * gridSize * gridSize;

            LODMeshData lodData = new(gridVolume, Allocator.TempJob);

            VoxelDownsampleJob downsampleJob = new()
            {
                SourceData = chunk.Data, StateTable = _nativeStateRegistry.States, Scale = scale, OutputData = lodData.DownsampledData,
            };

            JobHandle downsampleHandle = downsampleJob.Schedule();

            int lodScaleIndex = lodLevel; // 1=x2, 2=x4, 3=x8

            LODGreedyMeshJob meshJob = new()
            {
                Data = lodData.DownsampledData,
                StateTable = _nativeStateRegistry.States,
                AtlasEntries = _nativeAtlasLookup.Entries,
                GridSize = gridSize,
                LODScaleIndex = lodScaleIndex,
                ChunkCoord = chunk.Coord,
                Vertices = lodData.Vertices,
                Indices = lodData.Indices,
            };

            JobHandle meshHandle = meshJob.Schedule(downsampleHandle);
            chunk.ActiveJobHandle = meshHandle;

            _pendingLODMeshes.Add(new PendingLODMesh
            {
                Coord = chunk.Coord, Handle = meshHandle, Data = lodData, LODLevel = lodLevel,
            });
            _pipelineStats.IncrLODScheduled();
        }

        /// <summary>Tracks an in-flight LOD mesh job and its associated data for disposal on completion.</summary>
        private struct PendingLODMesh
        {
            /// <summary>World chunk coordinate this LOD mesh is being generated for.</summary>
            public int3 Coord;

            /// <summary>Job system handle for the downsample + mesh job chain.</summary>
            public JobHandle Handle;

            /// <summary>Native containers holding downsampled voxel data and mesh output.</summary>
            public LODMeshData Data;

            /// <summary>LOD level (1, 2, or 3) determining the voxel merge scale.</summary>
            public int LODLevel;
        }
    }
}
