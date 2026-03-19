using System;

using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;

using Unity.Collections;

namespace Lithforge.Meshing
{
    /// <summary>
    ///     Owns all NativeContainers needed for a single greedy mesh job:
    ///     vertex/index output lists and 6 neighbor border arrays.
    ///     Allocator: TempJob. Lifetime: job flight. Dispose: PollMeshJobs after Complete().
    /// </summary>
    public struct GreedyMeshData : IDisposable
    {
        public NativeList<PackedMeshVertex> OpaqueVertices;

        public NativeList<int> OpaqueIndices;

        public NativeList<PackedMeshVertex> CutoutVertices;

        public NativeList<int> CutoutIndices;

        public NativeList<PackedMeshVertex> TranslucentVertices;

        public NativeList<int> TranslucentIndices;

        public NativeArray<StateId> NeighborPosX;

        public NativeArray<StateId> NeighborNegX;

        public NativeArray<StateId> NeighborPosY;

        public NativeArray<StateId> NeighborNegY;

        public NativeArray<StateId> NeighborPosZ;

        public NativeArray<StateId> NeighborNegZ;

        /// <summary>
        ///     Neighbor liquid ghost slabs for corner level interpolation (Luanti-style).
        ///     Each slab is Size*Size (1024 bytes), indexed [y * Size + edgeCoord].
        ///     Contains raw LiquidCell bytes from the boundary slice of each neighbor chunk.
        ///     When a neighbor has no liquid data, the slab is all zeros (empty).
        ///     Owner: GreedyMeshData. Lifetime: job flight. Allocator: TempJob.
        /// </summary>
        public NativeArray<byte> LiquidNeighborPosX;

        public NativeArray<byte> LiquidNeighborNegX;

        public NativeArray<byte> LiquidNeighborPosZ;

        public NativeArray<byte> LiquidNeighborNegZ;

        public GreedyMeshData(Allocator allocator)
        {
            OpaqueVertices = new NativeList<PackedMeshVertex>(4096, allocator);
            OpaqueIndices = new NativeList<int>(6144, allocator);
            CutoutVertices = new NativeList<PackedMeshVertex>(512, allocator);
            CutoutIndices = new NativeList<int>(768, allocator);
            TranslucentVertices = new NativeList<PackedMeshVertex>(1024, allocator);
            TranslucentIndices = new NativeList<int>(1536, allocator);
            NeighborPosX = new NativeArray<StateId>(ChunkConstants.SizeSquared, allocator);
            NeighborNegX = new NativeArray<StateId>(ChunkConstants.SizeSquared, allocator);
            NeighborPosY = new NativeArray<StateId>(ChunkConstants.SizeSquared, allocator);
            NeighborNegY = new NativeArray<StateId>(ChunkConstants.SizeSquared, allocator);
            NeighborPosZ = new NativeArray<StateId>(ChunkConstants.SizeSquared, allocator);
            NeighborNegZ = new NativeArray<StateId>(ChunkConstants.SizeSquared, allocator);
            LiquidNeighborPosX = new NativeArray<byte>(ChunkConstants.SizeSquared, allocator);
            LiquidNeighborNegX = new NativeArray<byte>(ChunkConstants.SizeSquared, allocator);
            LiquidNeighborPosZ = new NativeArray<byte>(ChunkConstants.SizeSquared, allocator);
            LiquidNeighborNegZ = new NativeArray<byte>(ChunkConstants.SizeSquared, allocator);
        }

        public void Dispose()
        {
            if (OpaqueVertices.IsCreated)
            {
                OpaqueVertices.Dispose();
            }

            if (OpaqueIndices.IsCreated)
            {
                OpaqueIndices.Dispose();
            }

            if (CutoutVertices.IsCreated)
            {
                CutoutVertices.Dispose();
            }

            if (CutoutIndices.IsCreated)
            {
                CutoutIndices.Dispose();
            }

            if (TranslucentVertices.IsCreated)
            {
                TranslucentVertices.Dispose();
            }

            if (TranslucentIndices.IsCreated)
            {
                TranslucentIndices.Dispose();
            }

            if (NeighborPosX.IsCreated)
            {
                NeighborPosX.Dispose();
            }

            if (NeighborNegX.IsCreated)
            {
                NeighborNegX.Dispose();
            }

            if (NeighborPosY.IsCreated)
            {
                NeighborPosY.Dispose();
            }

            if (NeighborNegY.IsCreated)
            {
                NeighborNegY.Dispose();
            }

            if (NeighborPosZ.IsCreated)
            {
                NeighborPosZ.Dispose();
            }

            if (NeighborNegZ.IsCreated)
            {
                NeighborNegZ.Dispose();
            }

            if (LiquidNeighborPosX.IsCreated)
            {
                LiquidNeighborPosX.Dispose();
            }

            if (LiquidNeighborNegX.IsCreated)
            {
                LiquidNeighborNegX.Dispose();
            }

            if (LiquidNeighborPosZ.IsCreated)
            {
                LiquidNeighborPosZ.Dispose();
            }

            if (LiquidNeighborNegZ.IsCreated)
            {
                LiquidNeighborNegZ.Dispose();
            }
        }
    }
}
