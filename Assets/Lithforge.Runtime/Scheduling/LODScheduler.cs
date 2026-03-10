using System.Collections.Generic;
using Lithforge.Meshing;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Rendering;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lithforge.Runtime.Scheduling
{
    /// <summary>
    /// Owns the LOD mesh job queue: assigning LOD levels based on camera distance,
    /// scheduling downsample + mesh jobs, polling for completion, and disposing resources.
    /// </summary>
    public sealed class LODScheduler
    {
        private readonly List<PendingLODMesh> _pendingLODMeshes = new List<PendingLODMesh>();
        private readonly ChunkManager _chunkManager;
        private readonly NativeStateRegistry _nativeStateRegistry;
        private readonly NativeAtlasLookup _nativeAtlasLookup;
        private readonly ChunkRenderManager _chunkRenderManager;
        private readonly ChunkCulling _culling;
        private readonly int _maxLODMeshesPerFrame;
        private readonly int _lod1Distance;
        private readonly int _lod2Distance;
        private readonly int _lod3Distance;

        // Reusable cache to avoid per-frame allocation
        private readonly List<ManagedChunk> _readyChunksCache = new List<ManagedChunk>();

        public int PendingCount
        {
            get { return _pendingLODMeshes.Count; }
        }

        public LODScheduler(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            NativeAtlasLookup nativeAtlasLookup,
            ChunkRenderManager chunkRenderManager,
            ChunkCulling culling,
            int maxLODMeshesPerFrame,
            int lod1Distance,
            int lod2Distance,
            int lod3Distance)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
            _nativeAtlasLookup = nativeAtlasLookup;
            _chunkRenderManager = chunkRenderManager;
            _culling = culling;
            _maxLODMeshesPerFrame = maxLODMeshesPerFrame;
            _lod1Distance = lod1Distance;
            _lod2Distance = lod2Distance;
            _lod3Distance = lod3Distance;
        }

        public void PollCompleted()
        {
            for (int i = _pendingLODMeshes.Count - 1; i >= 0; i--)
            {
                PendingLODMesh pending = _pendingLODMeshes[i];

                if (pending.Handle.IsCompleted)
                {
                    pending.Handle.Complete();

                    // Upload as single-submesh opaque mesh
                    _chunkRenderManager.UpdateRendererSingleMesh(
                        pending.Coord,
                        pending.Data.Vertices,
                        pending.Data.Indices);

                    pending.Data.Dispose();

                    ManagedChunk chunk = _chunkManager.GetChunk(pending.Coord);

                    if (chunk != null)
                    {
                        chunk.RenderedLODLevel = pending.LODLevel;
                    }

                    _pendingLODMeshes.RemoveAt(i);
                }
            }
        }

        public void UpdateLODLevels(int3 cameraChunkCoord)
        {
            _chunkManager.FillReadyChunks(_readyChunksCache);

            for (int i = 0; i < _readyChunksCache.Count; i++)
            {
                ManagedChunk chunk = _readyChunksCache[i];
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

                    // If transitioning to LOD0, trigger full remesh
                    if (desiredLOD == 0 && chunk.State == ChunkState.Ready)
                    {
                        chunk.State = ChunkState.Generated;
                    }
                }
            }
        }

        public void ScheduleJobs()
        {
            int slotsAvailable = _maxLODMeshesPerFrame - _pendingLODMeshes.Count;

            if (slotsAvailable <= 0)
            {
                return;
            }

            _chunkManager.FillReadyChunks(_readyChunksCache);
            int scheduled = 0;

            for (int i = 0; i < _readyChunksCache.Count && scheduled < slotsAvailable; i++)
            {
                ManagedChunk chunk = _readyChunksCache[i];

                // Only schedule LOD mesh if LOD level changed and chunk is ready
                if (chunk.LODLevel == 0 || chunk.LODLevel == chunk.RenderedLODLevel)
                {
                    continue;
                }

                if (chunk.State != ChunkState.Ready)
                {
                    continue;
                }

                // Skip if a LOD job is already in flight for this chunk
                bool lodPending = false;

                for (int j = 0; j < _pendingLODMeshes.Count; j++)
                {
                    if (_pendingLODMeshes[j].Coord.Equals(chunk.Coord))
                    {
                        lodPending = true;
                        break;
                    }
                }

                if (lodPending)
                {
                    continue;
                }

                if (!_culling.IsInFrustum(chunk.Coord))
                {
                    continue;
                }

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

                LODMeshJob meshJob = new LODMeshJob
                {
                    Data = lodData.DownsampledData,
                    StateTable = _nativeStateRegistry.States,
                    AtlasEntries = _nativeAtlasLookup.Entries,
                    GridSize = gridSize,
                    VoxelScale = scale,
                    Vertices = lodData.Vertices,
                    Indices = lodData.Indices,
                };

                JobHandle meshHandle = meshJob.Schedule(downsampleHandle);

                _pendingLODMeshes.Add(new PendingLODMesh
                {
                    Coord = chunk.Coord,
                    Handle = meshHandle,
                    Data = lodData,
                    LODLevel = lodLevel,
                });

                scheduled++;
            }
        }

        public void CleanupCoord(int3 coord)
        {
            for (int i = _pendingLODMeshes.Count - 1; i >= 0; i--)
            {
                if (_pendingLODMeshes[i].Coord.Equals(coord))
                {
                    _pendingLODMeshes[i].Handle.Complete();
                    _pendingLODMeshes[i].Data.Dispose();
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
