using Lithforge.Meshing;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lithforge.Runtime.Rendering
{
    public static class MeshUploader
    {
        public static void Upload(Mesh target, NativeList<MeshVertex> verts, NativeList<int> indices)
        {
            target.Clear();

            if (verts.Length == 0)
            {
                return;
            }

            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = meshDataArray[0];

            meshData.SetVertexBufferParams(verts.Length, MeshVertex.VertexAttributes);
            meshData.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);

            NativeArray<MeshVertex> vertexBuffer = meshData.GetVertexData<MeshVertex>();
            NativeArray<int> indexBuffer = meshData.GetIndexData<int>();

            NativeArray<MeshVertex>.Copy(verts.AsArray(), vertexBuffer, verts.Length);
            NativeArray<int>.Copy(indices.AsArray(), indexBuffer, indices.Length);

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Triangles),
                MeshUpdateFlags.DontValidateIndices);

            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, target,
                MeshUpdateFlags.DontValidateIndices);
        }
    }
}
