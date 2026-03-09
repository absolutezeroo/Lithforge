using System;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Unity.Collections;

namespace Lithforge.Meshing
{
    /// <summary>
    /// Owns all NativeContainers needed for a single greedy mesh job:
    /// vertex/index output lists and 6 neighbor border arrays.
    /// Allocator: TempJob. Lifetime: job flight. Dispose: PollMeshJobs after Complete().
    /// </summary>
    public struct GreedyMeshData : IDisposable
    {
        public NativeList<MeshVertex> OpaqueVertices;
        public NativeList<int> OpaqueIndices;
        public NativeList<MeshVertex> TranslucentVertices;
        public NativeList<int> TranslucentIndices;
        public NativeArray<StateId> NeighborPosX;
        public NativeArray<StateId> NeighborNegX;
        public NativeArray<StateId> NeighborPosY;
        public NativeArray<StateId> NeighborNegY;
        public NativeArray<StateId> NeighborPosZ;
        public NativeArray<StateId> NeighborNegZ;

        public GreedyMeshData(Allocator allocator)
        {
            OpaqueVertices = new NativeList<MeshVertex>(4096, allocator);
            OpaqueIndices = new NativeList<int>(6144, allocator);
            TranslucentVertices = new NativeList<MeshVertex>(1024, allocator);
            TranslucentIndices = new NativeList<int>(1536, allocator);
            NeighborPosX = new NativeArray<StateId>(ChunkConstants.SizeSquared, allocator, NativeArrayOptions.ClearMemory);
            NeighborNegX = new NativeArray<StateId>(ChunkConstants.SizeSquared, allocator, NativeArrayOptions.ClearMemory);
            NeighborPosY = new NativeArray<StateId>(ChunkConstants.SizeSquared, allocator, NativeArrayOptions.ClearMemory);
            NeighborNegY = new NativeArray<StateId>(ChunkConstants.SizeSquared, allocator, NativeArrayOptions.ClearMemory);
            NeighborPosZ = new NativeArray<StateId>(ChunkConstants.SizeSquared, allocator, NativeArrayOptions.ClearMemory);
            NeighborNegZ = new NativeArray<StateId>(ChunkConstants.SizeSquared, allocator, NativeArrayOptions.ClearMemory);
        }

        public void Dispose()
        {
            if (OpaqueVertices.IsCreated) { OpaqueVertices.Dispose(); }
            if (OpaqueIndices.IsCreated) { OpaqueIndices.Dispose(); }
            if (TranslucentVertices.IsCreated) { TranslucentVertices.Dispose(); }
            if (TranslucentIndices.IsCreated) { TranslucentIndices.Dispose(); }
            if (NeighborPosX.IsCreated) { NeighborPosX.Dispose(); }
            if (NeighborNegX.IsCreated) { NeighborNegX.Dispose(); }
            if (NeighborPosY.IsCreated) { NeighborPosY.Dispose(); }
            if (NeighborNegY.IsCreated) { NeighborNegY.Dispose(); }
            if (NeighborPosZ.IsCreated) { NeighborPosZ.Dispose(); }
            if (NeighborNegZ.IsCreated) { NeighborNegZ.Dispose(); }
        }
    }
}
