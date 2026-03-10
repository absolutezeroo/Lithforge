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
    /// Owns the mesh job queue: scheduling greedy mesh jobs for LOD0 chunks,
    /// polling for completion, uploading to ChunkRenderManager, and disposing resources.
    /// </summary>
    public sealed class MeshScheduler
    {
        private readonly List<PendingMesh> _pendingMeshes = new List<PendingMesh>();
        private readonly ChunkManager _chunkManager;
        private readonly NativeStateRegistry _nativeStateRegistry;
        private readonly NativeAtlasLookup _nativeAtlasLookup;
        private readonly ChunkRenderManager _chunkRenderManager;
        private readonly ChunkCulling _culling;
        private readonly int _maxMeshesPerFrame;

        public int PendingCount
        {
            get { return _pendingMeshes.Count; }
        }

        public MeshScheduler(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            NativeAtlasLookup nativeAtlasLookup,
            ChunkRenderManager chunkRenderManager,
            ChunkCulling culling,
            int maxMeshesPerFrame)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
            _nativeAtlasLookup = nativeAtlasLookup;
            _chunkRenderManager = chunkRenderManager;
            _culling = culling;
            _maxMeshesPerFrame = maxMeshesPerFrame;
        }

        public void PollCompleted()
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

        /// <summary>
        /// Schedules greedy mesh jobs for Generated chunks.
        /// When applyFilters is true, frustum culling and LOD0-only filtering are applied.
        /// During spawn (applyFilters=false), all Generated chunks are meshed unconditionally.
        /// </summary>
        public void ScheduleJobs(bool applyFilters)
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

                if (applyFilters)
                {
                    // Frustum culling: skip meshing for chunks outside the camera frustum
                    if (!_culling.IsInFrustum(chunk.Coord))
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

        public void CleanupCoord(int3 coord)
        {
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

        public void Shutdown()
        {
            for (int i = 0; i < _pendingMeshes.Count; i++)
            {
                _pendingMeshes[i].Handle.Complete();
                _pendingMeshes[i].Data.Dispose();
            }

            _pendingMeshes.Clear();
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

        private struct PendingMesh
        {
            public int3 Coord;
            public JobHandle Handle;
            public GreedyMeshData Data;
        }
    }
}
