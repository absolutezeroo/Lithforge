using System;
using Lithforge.Voxel.Block;
using Unity.Collections;

namespace Lithforge.Meshing
{
    /// <summary>
    /// Owns all NativeContainers needed for a single LOD mesh job flight:
    /// downsampled data buffer and vertex/index output lists.
    /// Allocator: TempJob. Lifetime: job flight. Dispose: after job completion on main thread.
    /// </summary>
    public struct LODMeshData : IDisposable
    {
        /// <summary>Downsampled voxel data after VoxelDownsampleJob runs.</summary>
        public NativeArray<StateId> DownsampledData;

        /// <summary>Vertex output buffer for the LOD greedy mesh.</summary>
        public NativeList<PackedMeshVertex> Vertices;

        /// <summary>Index output buffer for the LOD greedy mesh.</summary>
        public NativeList<int> Indices;

        /// <summary>Allocates downsample buffer and vertex/index lists for a LOD mesh flight.</summary>
        public LODMeshData(int gridVolume, Allocator allocator)
        {
            DownsampledData = new NativeArray<StateId>(gridVolume, allocator, NativeArrayOptions.ClearMemory);
            Vertices = new NativeList<PackedMeshVertex>(1024, allocator);
            Indices = new NativeList<int>(1536, allocator);
        }

        /// <summary>Disposes all NativeContainers if they were created.</summary>
        public void Dispose()
        {
            if (DownsampledData.IsCreated)
            {
                DownsampledData.Dispose();
            }

            if (Vertices.IsCreated)
            {
                Vertices.Dispose();
            }

            if (Indices.IsCreated)
            {
                Indices.Dispose();
            }
        }
    }
}
