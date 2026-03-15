using System;
using Unity.Collections;

namespace Lithforge.Meshing
{
    /// <summary>
    /// Output container for a single mesh produced by greedy meshing jobs.
    /// Owns the vertex and index NativeLists and disposes them together.
    /// <remarks>
    /// Allocator lifetime must match usage: TempJob for per-frame job output,
    /// Persistent for long-lived cached meshes.
    /// </remarks>
    /// </summary>
    public struct MeshData : IDisposable
    {
        /// <summary>Vertex buffer populated by meshing jobs.</summary>
        public NativeList<MeshVertex> Vertices;

        /// <summary>Triangle index buffer (3 indices per triangle) referencing into Vertices.</summary>
        public NativeList<int> Indices;

        /// <summary>
        /// Allocates vertex and index lists with sensible initial capacities.
        /// </summary>
        /// <param name="allocator">NativeContainer allocator (TempJob for job output, Persistent for caching).</param>
        public MeshData(Allocator allocator)
        {
            Vertices = new NativeList<MeshVertex>(4096, allocator);
            Indices = new NativeList<int>(6144, allocator);
        }

        /// <summary>Disposes both NativeLists if they are still allocated.</summary>
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
