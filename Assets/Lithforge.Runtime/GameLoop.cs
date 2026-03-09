using System.Collections.Generic;
using Lithforge.Meshing;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Rendering;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
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
        private long _seed;

        private readonly List<PendingGeneration> _pendingGenerations = new List<PendingGeneration>();
        private readonly List<PendingMesh> _pendingMeshes = new List<PendingMesh>();
        private readonly List<int3> _unloadedCoords = new List<int3>();

        private const int _maxGenerationsPerFrame = 4;
        private const int _maxMeshesPerFrame = 4;

        private bool _initialized;

        public int PendingGenerationCount
        {
            get { return _pendingGenerations.Count; }
        }

        public int PendingMeshCount
        {
            get { return _pendingMeshes.Count; }
        }

        public void Initialize(
            ChunkManager chunkManager,
            GenerationPipeline generationPipeline,
            NativeStateRegistry nativeStateRegistry,
            NativeAtlasLookup nativeAtlasLookup,
            ChunkRenderManager chunkRenderManager,
            long seed)
        {
            _chunkManager = chunkManager;
            _generationPipeline = generationPipeline;
            _nativeStateRegistry = nativeStateRegistry;
            _nativeAtlasLookup = nativeAtlasLookup;
            _chunkRenderManager = chunkRenderManager;
            _seed = seed;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            PollGenerationJobs();
            PollMeshJobs();

            int3 cameraChunkCoord = GetCameraChunkCoord();

            _chunkManager.UpdateLoadingQueue(cameraChunkCoord);
            _chunkManager.UnloadDistantChunks(cameraChunkCoord, _unloadedCoords);

            for (int i = 0; i < _unloadedCoords.Count; i++)
            {
                int3 coord = _unloadedCoords[i];
                _chunkRenderManager.DestroyRenderer(coord);
                CleanupPendingJobsForCoord(coord);
            }

            ScheduleGenerationJobs();
            ScheduleMeshJobs();
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
                        // Transfer LightData ownership to chunk
                        chunk.LightData = pending.Handle.LightData;
                        chunk.State = ChunkState.Generated;
                        chunk.ActiveJobHandle = default;
                        pending.Handle.Dispose();
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
                        pending.Data.OpaqueIndices);

                    pending.Data.Dispose();

                    ManagedChunk chunk = _chunkManager.GetChunk(pending.Coord);

                    if (chunk != null)
                    {
                        chunk.State = ChunkState.Ready;
                        chunk.ActiveJobHandle = default;
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
    }
}
