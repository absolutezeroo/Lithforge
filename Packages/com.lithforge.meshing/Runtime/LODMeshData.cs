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
        public NativeArray<StateId> DownsampledData;
        public NativeList<MeshVertex> Vertices;
        public NativeList<int> Indices;

        public LODMeshData(int gridVolume, Allocator allocator)
        {
            DownsampledData = new NativeArray<StateId>(gridVolume, allocator, NativeArrayOptions.ClearMemory);
            Vertices = new NativeList<MeshVertex>(1024, allocator);
            Indices = new NativeList<int>(1536, allocator);
        }

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
