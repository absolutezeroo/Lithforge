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
        /// <summary>Opaque submesh vertex output buffer.</summary>
        public NativeList<PackedMeshVertex> OpaqueVertices;

        /// <summary>Opaque submesh index output buffer.</summary>
        public NativeList<int> OpaqueIndices;

        /// <summary>Cutout (alpha-tested) submesh vertex output buffer.</summary>
        public NativeList<PackedMeshVertex> CutoutVertices;

        /// <summary>Cutout submesh index output buffer.</summary>
        public NativeList<int> CutoutIndices;

        /// <summary>Translucent submesh vertex output buffer.</summary>
        public NativeList<PackedMeshVertex> TranslucentVertices;

        /// <summary>Translucent submesh index output buffer.</summary>
        public NativeList<int> TranslucentIndices;

        /// <summary>Boundary slab of +X neighbor chunk for cross-chunk face culling.</summary>
        public NativeArray<StateId> NeighborPosX;

        /// <summary>Boundary slab of -X neighbor chunk for cross-chunk face culling.</summary>
        public NativeArray<StateId> NeighborNegX;

        /// <summary>Boundary slab of +Y neighbor chunk for cross-chunk face culling.</summary>
        public NativeArray<StateId> NeighborPosY;

        /// <summary>Boundary slab of -Y neighbor chunk for cross-chunk face culling.</summary>
        public NativeArray<StateId> NeighborNegY;

        /// <summary>Boundary slab of +Z neighbor chunk for cross-chunk face culling.</summary>
        public NativeArray<StateId> NeighborPosZ;

        /// <summary>Boundary slab of -Z neighbor chunk for cross-chunk face culling.</summary>
        public NativeArray<StateId> NeighborNegZ;

        /// <summary>
        ///     Neighbor liquid ghost slabs for corner level interpolation (Luanti-style).
        ///     Each slab is Size*Size (1024 bytes), indexed [y * Size + edgeCoord].
        ///     Contains raw LiquidCell bytes from the boundary slice of each neighbor chunk.
        ///     When a neighbor has no liquid data, the slab is all zeros (empty).
        ///     Owner: GreedyMeshData. Lifetime: job flight. Allocator: TempJob.
        /// </summary>
        public NativeArray<byte> LiquidNeighborPosX;

        /// <summary>Liquid ghost slab for -X neighbor chunk boundary.</summary>
        public NativeArray<byte> LiquidNeighborNegX;

        /// <summary>Liquid ghost slab for +Z neighbor chunk boundary.</summary>
        public NativeArray<byte> LiquidNeighborPosZ;

        /// <summary>Liquid ghost slab for -Z neighbor chunk boundary.</summary>
        public NativeArray<byte> LiquidNeighborNegZ;

        /// <summary>Allocates all vertex/index lists and neighbor border arrays with the given allocator.</summary>
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

        /// <summary>Disposes all NativeContainers if they were created.</summary>
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
