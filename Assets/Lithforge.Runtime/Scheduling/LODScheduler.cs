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
    /// Owns the LOD mesh job queue: assigning LOD levels based on camera distance,
    /// scheduling downsample + mesh jobs, polling for completion, and disposing resources.
    /// Handles both first-time LOD meshing (Generated chunks) and LOD transitions (Ready chunks).
    /// </summary>
    public sealed class LODScheduler
    {
        private readonly List<PendingLODMesh> _pendingLODMeshes = new List<PendingLODMesh>();
        private readonly List<PendingLODMesh> _pendingLODDisposals = new List<PendingLODMesh>();
        private readonly ChunkManager _chunkManager;
        private readonly NativeStateRegistry _nativeStateRegistry;
        private readonly NativeAtlasLookup _nativeAtlasLookup;
        private readonly ChunkMeshStore _chunkMeshStore;
        private readonly ChunkCulling _culling;
        private readonly IPipelineStats _pipelineStats;
        private int _maxLODMeshesPerFrame;
        private int _maxLODCompletionsPerFrame;
        private readonly float _completionBudgetMs;
        private int _lod1Distance;
        private int _lod2Distance;
        private int _lod3Distance;

        // Reusable caches to avoid per-frame allocation
        private readonly List<ManagedChunk> _readyChunksCache = new List<ManagedChunk>();
        private readonly List<ManagedChunk> _generatedLODCache = new List<ManagedChunk>();
        private readonly List<ManagedChunk> _generatedChunksCache = new List<ManagedChunk>();

        public int PendingCount
        {
            get { return _pendingLODMeshes.Count; }
        }

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

        public void UpdateConfig(int renderDistance)
        {
            _maxLODMeshesPerFrame = SchedulingConfig.MaxLODMeshesPerFrame(renderDistance);
            _maxLODCompletionsPerFrame = SchedulingConfig.MaxLODCompletionsPerFrame(renderDistance);
            _lod1Distance = SchedulingConfig.LOD1Distance(renderDistance);
            _lod2Distance = SchedulingConfig.LOD2Distance(renderDistance);
            _lod3Distance = SchedulingConfig.LOD3Distance(renderDistance);
        }

        public void PollCompleted()
        {
            Profiler.BeginSample("LOD.PollCompleted");

            PollPendingLODDisposals();

            FrameBudget budget = new FrameBudget(_completionBudgetMs);
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
        /// Assigns LOD levels to both Ready and Generated chunks based on camera distance.
        /// Must be called before MeshScheduler.ScheduleJobs and LODScheduler.ScheduleJobs
        /// so that chunks get their LOD level before the schedulers decide who to mesh.
        /// </summary>
        public void UpdateLODLevels(int3 cameraChunkCoord)
        {
            // Always update Ready chunks every frame (LOD transitions on meshed chunks)
            _chunkManager.FillReadyChunks(_readyChunksCache);
            AssignLODLevels(_readyChunksCache, cameraChunkCoord);

            _chunkManager.FillGeneratedChunks(_generatedChunksCache);
            AssignLODLevels(_generatedChunksCache, cameraChunkCoord);
        }

        /// <summary>
        /// Schedules LOD mesh jobs from two sources:
        /// 1. Generated chunks with LODLevel > 0 (first-time LOD mesh)
        /// 2. Ready chunks with changed LOD level (LOD transition)
        /// MUST be called after UpdateLODLevels() in the same frame,
        /// as it reuses _readyChunksCache populated there.
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
        /// Drains the lazy disposal queue for LOD mesh jobs. Jobs that were in-flight
        /// when their chunk was unloaded are polled here — once complete, their TempJob
        /// data is disposed. Called at the start of PollCompleted each frame.
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

        private void AssignLODLevels(List<ManagedChunk> chunks, int3 cameraChunkCoord)
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                ManagedChunk chunk = chunks[i];
                int3 diff = chunk.Coord - cameraChunkCoord;
                int xzDist = math.max(math.abs(diff.x), math.abs(diff.z));

                int desiredLOD;

                if (xzDist >= _lod3Distance)
                {
                    desiredLOD = 3;
                }
                else if (xzDist >= _lod2Distance)
                {
                    desiredLOD = 2;
                }
                else if (xzDist >= _lod1Distance)
                {
                    desiredLOD = 1;
                }
                else
                {
                    desiredLOD = 0;
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

        private void ScheduleLODMesh(ManagedChunk chunk)
        {
            int lodLevel = chunk.LODLevel;
            int scale = 1 << lodLevel; // 2, 4, or 8
            int gridSize = ChunkConstants.Size / scale;
            int gridVolume = gridSize * gridSize * gridSize;

            LODMeshData lodData = new LODMeshData(gridVolume, Allocator.TempJob);

            VoxelDownsampleJob downsampleJob = new VoxelDownsampleJob
            {
                SourceData = chunk.Data,
                StateTable = _nativeStateRegistry.States,
                Scale = scale,
                OutputData = lodData.DownsampledData,
            };

            JobHandle downsampleHandle = downsampleJob.Schedule();

            int lodScaleIndex = lodLevel; // 1=x2, 2=x4, 3=x8

            LODGreedyMeshJob meshJob = new LODGreedyMeshJob
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
                Coord = chunk.Coord,
                Handle = meshHandle,
                Data = lodData,
                LODLevel = lodLevel,
            });
            _pipelineStats.IncrLODScheduled();
        }

        private struct PendingLODMesh
        {
            public int3 Coord;
            public JobHandle Handle;
            public LODMeshData Data;
            public int LODLevel;
        }
    }
}
