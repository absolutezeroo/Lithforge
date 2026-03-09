using System;
using Unity.Collections;

namespace Lithforge.Meshing
{
    public struct MeshData : IDisposable
    {
        public NativeList<MeshVertex> Vertices;
        public NativeList<int> Indices;

        public MeshData(Allocator allocator)
        {
            Vertices = new NativeList<MeshVertex>(4096, allocator);
            Indices = new NativeList<int>(6144, allocator);
        }

        public void Dispose()
        {
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
