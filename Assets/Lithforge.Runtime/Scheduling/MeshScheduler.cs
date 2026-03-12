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
    /// Also processes RelightPending chunks before allowing meshing.
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
            int maxMeshCompletionsPerFrame)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
            _nativeAtlasLookup = nativeAtlasLookup;
            _chunkMeshStore = chunkMeshStore;
            _culling = culling;
            _maxMeshesPerFrame = maxMeshesPerFrame;
            _maxMeshCompletionsPerFrame = maxMeshCompletionsPerFrame;
        }

        public void PollCompleted()
        {
            PollPendingDisposals();

            int completedThisFrame = 0;
            int i = 0;

            while (i < _pendingMeshes.Count)
            {
                if (completedThisFrame >= _maxMeshCompletionsPerFrame)
                {
                    break;
                }

                PendingMesh pending = _pendingMeshes[i];

                if (pending.Handle.IsCompleted)
                {
                    completedThisFrame++;
                    pending.Handle.Complete();

                    _chunkMeshStore.UpdateRenderer(
                        pending.Coord,
                        pending.Data.OpaqueVertices,
                        pending.Data.OpaqueIndices,
                        pending.Data.CutoutVertices,
                        pending.Data.CutoutIndices,
                        pending.Data.TranslucentVertices,
                        pending.Data.TranslucentIndices);

                    pending.Data.Dispose();
                    PipelineStats.IncrMeshCompleted();

                    ManagedChunk chunk = _chunkManager.GetChunk(pending.Coord);

                    if (chunk != null)
                    {
                        if (chunk.NeedsRemesh)
                        {
                            // Neighbor generated while we were meshing — redo with correct borders
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

                    _pendingMeshes.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
        }

        /// <summary>
        /// Processes RelightPending chunks by running LightRemovalJob synchronously.
        /// After relighting, transitions the chunk to Generated so it can be meshed.
        /// Should be called each frame before ScheduleJobs().
        /// </summary>
        public void ProcessRelightPending()
        {
            _chunkManager.FillChunksNeedingRelight(_relightCache);

            if (_relightCache.Count == 0)
            {
                return;
            }

            // Transition invalid chunks immediately, pack valid ones to front
            int validCount = 0;

            for (int i = 0; i < _relightCache.Count; i++)
            {
                ManagedChunk chunk = _relightCache[i];

                if (!chunk.LightData.IsCreated || !chunk.Data.IsCreated)
                {
                    chunk.State = ChunkState.Generated;
                }
                else
                {
                    _relightCache[validCount] = chunk;
                    validCount++;
                }
            }

            if (validCount == 0)
            {
                return;
            }

            // All jobs use the same changed indices (full chunk rescan).
            // Since SetBlock doesn't track the exact changed index, we pass all voxels.
            NativeArray<int> allIndices = new NativeArray<int>(ChunkConstants.Volume, Allocator.TempJob);

            for (int idx = 0; idx < ChunkConstants.Volume; idx++)
            {
                allIndices[idx] = idx;
            }

            // Batch-schedule all relight jobs then complete once
            NativeArray<JobHandle> handles = new NativeArray<JobHandle>(validCount, Allocator.Temp);

            for (int i = 0; i < validCount; i++)
            {
                ManagedChunk chunk = _relightCache[i];

                LightRemovalJob removalJob = new LightRemovalJob
                {
                    LightData = chunk.LightData,
                    ChunkData = chunk.Data,
                    StateTable = _nativeStateRegistry.States,
                    ChangedIndices = allIndices,
                };

                handles[i] = removalJob.Schedule();
            }

            JobHandle combined = JobHandle.CombineDependencies(handles);
            combined.Complete();

            handles.Dispose();
            allIndices.Dispose();

            for (int i = 0; i < validCount; i++)
            {
                _relightCache[i].State = ChunkState.Generated;
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
            NativeArray<StateId>[] outputs = new NativeArray<StateId>[]
            {
                meshData.NeighborPosX,
                meshData.NeighborNegX,
                meshData.NeighborPosY,
                meshData.NeighborNegY,
                meshData.NeighborPosZ,
                meshData.NeighborNegZ,
            };

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
                    Output = outputs[_borderExtractions[i].OutputIndex],
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

        private struct PendingMesh
        {
            public int3 Coord;
            public JobHandle Handle;
            public GreedyMeshData Data;
        }
    }
}
