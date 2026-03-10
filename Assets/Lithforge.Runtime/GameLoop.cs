using System.Collections.Generic;
using Lithforge.Meshing;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Spawn;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;
using Lithforge.WorldGen.Decoration;
using Lithforge.WorldGen.Pipeline;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime
{
    public sealed class GameLoop : MonoBehaviour
    {
        private ChunkManager _chunkManager;
        private GenerationPipeline _generationPipeline;
        private NativeStateRegistry _nativeStateRegistry;
        private NativeAtlasLookup _nativeAtlasLookup;
        private ChunkRenderManager _chunkRenderManager;
        private DecorationStage _decorationStage;
        private WorldStorage _worldStorage;
        private long _seed;

        private readonly List<PendingGeneration> _pendingGenerations = new List<PendingGeneration>();
        private readonly List<PendingMesh> _pendingMeshes = new List<PendingMesh>();
        private readonly List<PendingLODMesh> _pendingLODMeshes = new List<PendingLODMesh>();
        private readonly List<int3> _unloadedCoords = new List<int3>();

        private int _maxGenerationsPerFrame = 4;
        private int _maxMeshesPerFrame = 4;
        private int _maxLODMeshesPerFrame = 2;
        private int _lod1Distance = 4;
        private int _lod2Distance = 8;
        private int _lod3Distance = 14;

        // Frustum culling
        private readonly Plane[] _frustumPlanes = new Plane[6];

        // Reusable cache to avoid per-frame allocation in LOD methods
        private readonly List<ManagedChunk> _readyChunksCache = new List<ManagedChunk>();

        private static readonly int3[] _neighborOffsets = new int3[]
        {
            new int3(1, 0, 0),
            new int3(-1, 0, 0),
            new int3(0, 1, 0),
            new int3(0, -1, 0),
            new int3(0, 0, 1),
            new int3(0, 0, -1),
        };

        private bool _initialized;
        private SpawnManager _spawnManager;

        public int PendingGenerationCount
        {
            get { return _pendingGenerations.Count; }
        }

        public int PendingMeshCount
        {
            get { return _pendingMeshes.Count; }
        }

        public int PendingLODMeshCount
        {
            get { return _pendingLODMeshes.Count; }
        }

        /// <summary>
        /// True once the spawn area chunks are fully meshed and the player has been placed.
        /// </summary>
        public bool SpawnReady
        {
            get { return _spawnManager != null && _spawnManager.IsComplete; }
        }

        public void Initialize(
            ChunkManager chunkManager,
            GenerationPipeline generationPipeline,
            NativeStateRegistry nativeStateRegistry,
            NativeAtlasLookup nativeAtlasLookup,
            ChunkRenderManager chunkRenderManager,
            DecorationStage decorationStage,
            WorldStorage worldStorage,
            long seed,
            ChunkSettings chunkSettings)
        {
            _chunkManager = chunkManager;
            _generationPipeline = generationPipeline;
            _nativeStateRegistry = nativeStateRegistry;
            _nativeAtlasLookup = nativeAtlasLookup;
            _chunkRenderManager = chunkRenderManager;
            _decorationStage = decorationStage;
            _worldStorage = worldStorage;
            _seed = seed;
            _maxGenerationsPerFrame = chunkSettings.MaxGenerationsPerFrame;
            _maxMeshesPerFrame = chunkSettings.MaxMeshesPerFrame;
            _maxLODMeshesPerFrame = chunkSettings.MaxLODMeshesPerFrame;
            _lod1Distance = chunkSettings.LOD1Distance;
            _lod2Distance = chunkSettings.LOD2Distance;
            _lod3Distance = chunkSettings.LOD3Distance;
            _initialized = true;
        }

        /// <summary>
        /// Sets the SpawnManager that coordinates spawn chunk loading and player placement.
        /// Must be called after Initialize.
        /// </summary>
        public void SetSpawnManager(SpawnManager spawnManager)
        {
            _spawnManager = spawnManager;
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            PollGenerationJobs();
            PollMeshJobs();
            PollLODMeshJobs();

            int3 cameraChunkCoord = GetCameraChunkCoord();

            // Update frustum planes for culling
            Camera cam = Camera.main;

            if (cam != null)
            {
                GeometryUtility.CalculateFrustumPlanes(cam, _frustumPlanes);
            }

            _chunkManager.UpdateLoadingQueue(cameraChunkCoord);
            _chunkManager.UnloadDistantChunks(cameraChunkCoord, _unloadedCoords);

            // Advance spawn state machine until complete
            if (_spawnManager != null && !_spawnManager.IsComplete)
            {
                _spawnManager.Tick();
            }

            for (int i = 0; i < _unloadedCoords.Count; i++)
            {
                int3 coord = _unloadedCoords[i];

                _chunkRenderManager.DestroyRenderer(coord);
                CleanupPendingJobsForCoord(coord);
            }

            ScheduleGenerationJobs();
            ScheduleMeshJobs();

            // LOD and frustum culling only activate after spawn is complete
            if (SpawnReady)
            {
                UpdateChunkLODLevels(cameraChunkCoord);
                ScheduleLODMeshJobs();
            }
        }

        private void PollGenerationJobs()
        {
            for (int i = _pendingGenerations.Count - 1; i >= 0; i--)
            {
                PendingGeneration pending = _pendingGenerations[i];

                if (pending.Handle.FinalHandle.IsCompleted)
                {
                    pending.Handle.FinalHandle.Complete();

                    ManagedChunk chunk = _chunkManager.GetChunk(pending.Coord);

                    if (chunk != null)
                    {
                        // Run decoration on main thread (managed code, not Burst)
                        if (_decorationStage != null &&
                            pending.Handle.BiomeMap.IsCreated &&
                            pending.Handle.HeightMap.IsCreated)
                        {
                            _decorationStage.Decorate(
                                pending.Coord,
                                chunk.Data,
                                pending.Handle.HeightMap,
                                pending.Handle.BiomeMap,
                                _seed);
                        }

                        // Transfer LightData ownership to chunk
                        chunk.LightData = pending.Handle.LightData;
                        chunk.State = ChunkState.Generated;
                        chunk.ActiveJobHandle = default;
                        pending.Handle.Dispose();

                        InvalidateReadyNeighbors(pending.Coord);
                    }
                    else
                    {
                        // Chunk was unloaded — dispose everything including LightData
                        pending.Handle.DisposeAll();
                    }

                    _pendingGenerations.RemoveAt(i);
                }
            }
        }

        private void PollMeshJobs()
        {
            for (int i = _pendingMeshes.Count - 1; i >= 0; i--)
            {
                PendingMesh pending = _pendingMeshes[i];

                if (pending.Handle.IsCompleted)
                {
                    pending.Handle.Complete();

                    _chunkRenderManager.UpdateRenderer(
                        pending.Coord,
                        pending.Data.OpaqueVertices,
                        pending.Data.OpaqueIndices,
                        pending.Data.CutoutVertices,
                        pending.Data.CutoutIndices,
                        pending.Data.TranslucentVertices,
                        pending.Data.TranslucentIndices);

                    pending.Data.Dispose();

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
            }
        }

        private void ScheduleGenerationJobs()
        {
            int slotsAvailable = _maxGenerationsPerFrame - _pendingGenerations.Count;

            if (slotsAvailable <= 0)
            {
                return;
            }

            List<ManagedChunk> chunks = _chunkManager.GetChunksToGenerate(slotsAvailable);

            for (int i = 0; i < chunks.Count; i++)
            {
                ManagedChunk chunk = chunks[i];

                // Try loading from storage first
                if (_worldStorage != null && _worldStorage.HasChunk(chunk.Coord))
                {
                    NativeArray<byte> lightData = new NativeArray<byte>(
                        Lithforge.Voxel.Chunk.ChunkConstants.Volume,
                        Allocator.Persistent, NativeArrayOptions.ClearMemory);

                    if (_worldStorage.LoadChunk(chunk.Coord, chunk.Data, lightData))
                    {
                        chunk.LightData = lightData;
                        chunk.State = ChunkState.Generated;
                        chunk.ActiveJobHandle = default;

                        InvalidateReadyNeighbors(chunk.Coord);

                        continue;
                    }

                    lightData.Dispose();
                }

                GenerationHandle handle = _generationPipeline.Schedule(chunk.Coord, _seed, chunk.Data);
                chunk.ActiveJobHandle = handle.FinalHandle;

                _pendingGenerations.Add(new PendingGeneration
                {
                    Coord = chunk.Coord,
                    Handle = handle,
                });
            }
        }

        private void ScheduleMeshJobs()
        {
            int slotsAvailable = _maxMeshesPerFrame - _pendingMeshes.Count;

            if (slotsAvailable <= 0)
            {
                return;
            }

            List<ManagedChunk> chunks = _chunkManager.GetChunksToMesh(slotsAvailable);

            for (int i = 0; i < chunks.Count; i++)
            {
                ManagedChunk chunk = chunks[i];

                // Skip frustum culling and LOD during spawn — all chunks must mesh
                if (SpawnReady)
                {
                    // Frustum culling: skip meshing for chunks outside the camera frustum
                    if (!IsChunkInFrustum(chunk.Coord))
                    {
                        continue;
                    }

                    // Only schedule full-detail mesh for LOD0 chunks
                    if (chunk.LODLevel > 0)
                    {
                        continue;
                    }
                }

                chunk.State = ChunkState.Meshing;

                GreedyMeshData meshData = new GreedyMeshData(Allocator.TempJob);

                // Extract neighbor border slices for cross-chunk culling
                ExtractNeighborBorders(chunk.Coord, meshData);

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

                JobHandle meshHandle = meshJob.Schedule();
                chunk.ActiveJobHandle = meshHandle;

                _pendingMeshes.Add(new PendingMesh
                {
                    Coord = chunk.Coord,
                    Handle = meshHandle,
                    Data = meshData,
                });
            }
        }

        private void ExtractNeighborBorders(int3 coord, GreedyMeshData meshData)
        {
            // +X neighbor: chunk at (x+1), extract its -X face (face 1)
            ExtractBorderFromNeighbor(coord + new int3(1, 0, 0), 1, meshData.NeighborPosX);
            // -X neighbor: chunk at (x-1), extract its +X face (face 0)
            ExtractBorderFromNeighbor(coord + new int3(-1, 0, 0), 0, meshData.NeighborNegX);
            // +Y neighbor: chunk at (y+1), extract its -Y face (face 3)
            ExtractBorderFromNeighbor(coord + new int3(0, 1, 0), 3, meshData.NeighborPosY);
            // -Y neighbor: chunk at (y-1), extract its +Y face (face 2)
            ExtractBorderFromNeighbor(coord + new int3(0, -1, 0), 2, meshData.NeighborNegY);
            // +Z neighbor: chunk at (z+1), extract its -Z face (face 5)
            ExtractBorderFromNeighbor(coord + new int3(0, 0, 1), 5, meshData.NeighborPosZ);
            // -Z neighbor: chunk at (z-1), extract its +Z face (face 4)
            ExtractBorderFromNeighbor(coord + new int3(0, 0, -1), 4, meshData.NeighborNegZ);
        }

        private void ExtractBorderFromNeighbor(int3 neighborCoord, int faceDirection, NativeArray<StateId> output)
        {
            ManagedChunk neighbor = _chunkManager.GetChunk(neighborCoord);

            if (neighbor != null && neighbor.State >= ChunkState.Generated && neighbor.Data.IsCreated)
            {
                ChunkBorderExtractor.ExtractBorder(neighbor.Data, faceDirection, output);
            }
            // If neighbor doesn't exist, output stays zero-initialized (air)
        }

        private void InvalidateReadyNeighbors(int3 coord)
        {
            for (int i = 0; i < _neighborOffsets.Length; i++)
            {
                int3 neighborCoord = coord + _neighborOffsets[i];
                ManagedChunk neighbor = _chunkManager.GetChunk(neighborCoord);

                if (neighbor == null)
                {
                    continue;
                }

                if (neighbor.State == ChunkState.Ready)
                {
                    neighbor.State = ChunkState.Generated;
                }
                else if (neighbor.State == ChunkState.Meshing)
                {
                    // Job is active — flag for re-mesh after it completes
                    neighbor.NeedsRemesh = true;
                }
            }
        }

        private void CleanupPendingJobsForCoord(int3 coord)
        {
            for (int i = _pendingGenerations.Count - 1; i >= 0; i--)
            {
                if (_pendingGenerations[i].Coord.Equals(coord))
                {
                    _pendingGenerations[i].Handle.FinalHandle.Complete();
                    _pendingGenerations[i].Handle.DisposeAll();
                    _pendingGenerations.RemoveAt(i);
                }
            }

            for (int i = _pendingMeshes.Count - 1; i >= 0; i--)
            {
                if (_pendingMeshes[i].Coord.Equals(coord))
                {
                    _pendingMeshes[i].Handle.Complete();
                    _pendingMeshes[i].Data.Dispose();
                    _pendingMeshes.RemoveAt(i);
                }
            }

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

        private int3 GetCameraChunkCoord()
        {
            Vector3 camPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;

            return new int3(
                (int)math.floor(camPos.x / ChunkConstants.Size),
                (int)math.floor(camPos.y / ChunkConstants.Size),
                (int)math.floor(camPos.z / ChunkConstants.Size));
        }

        public void Shutdown()
        {
            for (int i = 0; i < _pendingGenerations.Count; i++)
            {
                _pendingGenerations[i].Handle.FinalHandle.Complete();
                _pendingGenerations[i].Handle.DisposeAll();
            }

            _pendingGenerations.Clear();

            for (int i = 0; i < _pendingMeshes.Count; i++)
            {
                _pendingMeshes[i].Handle.Complete();
                _pendingMeshes[i].Data.Dispose();
            }

            _pendingMeshes.Clear();

            for (int i = 0; i < _pendingLODMeshes.Count; i++)
            {
                _pendingLODMeshes[i].Handle.Complete();
                _pendingLODMeshes[i].Data.Dispose();
            }

            _pendingLODMeshes.Clear();
        }

        private void UpdateChunkLODLevels(int3 cameraChunkCoord)
        {
            // Iterate all Ready chunks and assign LOD level based on distance
            // This is called each frame but only triggers re-mesh when LOD level changes
            _chunkManager.FillReadyChunks(_readyChunksCache);
            List<ManagedChunk> readyChunks = _readyChunksCache;

            for (int i = 0; i < readyChunks.Count; i++)
            {
                ManagedChunk chunk = readyChunks[i];
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

        private void ScheduleLODMeshJobs()
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

                if (!IsChunkInFrustum(chunk.Coord))
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

        private void PollLODMeshJobs()
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

        private bool IsChunkInFrustum(int3 chunkCoord)
        {
            Camera cam = Camera.main;

            if (cam == null)
            {
                return true;
            }

            // Compute chunk AABB in world space
            float3 min = new float3(
                chunkCoord.x * ChunkConstants.Size,
                chunkCoord.y * ChunkConstants.Size,
                chunkCoord.z * ChunkConstants.Size);
            float3 max = min + new float3(ChunkConstants.Size, ChunkConstants.Size, ChunkConstants.Size);

            Bounds bounds = new Bounds();
            bounds.SetMinMax(
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, max.y, max.z));

            return GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds);
        }

        private struct PendingGeneration
        {
            public int3 Coord;
            public GenerationHandle Handle;
        }

        private struct PendingMesh
        {
            public int3 Coord;
            public JobHandle Handle;
            public GreedyMeshData Data;
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
