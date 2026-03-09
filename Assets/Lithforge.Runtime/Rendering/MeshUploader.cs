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
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, target,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            target.RecalculateBounds();
        }

        public static void Upload(
            Mesh target,
            NativeList<MeshVertex> opaqueVerts, NativeList<int> opaqueIndices,
            NativeList<MeshVertex> translucentVerts, NativeList<int> translucentIndices)
        {
            target.Clear();

            int totalVerts = opaqueVerts.Length + translucentVerts.Length;
            int totalIndices = opaqueIndices.Length + translucentIndices.Length;

            if (totalVerts == 0)
            {
                return;
            }

            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = meshDataArray[0];

            meshData.SetVertexBufferParams(totalVerts, MeshVertex.VertexAttributes);
            meshData.SetIndexBufferParams(totalIndices, IndexFormat.UInt32);

            NativeArray<MeshVertex> vertexBuffer = meshData.GetVertexData<MeshVertex>();
            NativeArray<int> indexBuffer = meshData.GetIndexData<int>();

            // Copy opaque vertices
            if (opaqueVerts.Length > 0)
            {
                NativeArray<MeshVertex>.Copy(opaqueVerts.AsArray(), 0, vertexBuffer, 0, opaqueVerts.Length);
            }

            // Copy translucent vertices after opaque
            if (translucentVerts.Length > 0)
            {
                NativeArray<MeshVertex>.Copy(translucentVerts.AsArray(), 0, vertexBuffer, opaqueVerts.Length, translucentVerts.Length);
            }

            // Copy opaque indices directly
            if (opaqueIndices.Length > 0)
            {
                NativeArray<int>.Copy(opaqueIndices.AsArray(), 0, indexBuffer, 0, opaqueIndices.Length);
            }

            // Copy translucent indices with offset applied to vertex references
            int vertexOffset = opaqueVerts.Length;

            for (int i = 0; i < translucentIndices.Length; i++)
            {
                indexBuffer[opaqueIndices.Length + i] = translucentIndices[i] + vertexOffset;
            }

            bool hasTranslucent = translucentVerts.Length > 0;
            meshData.subMeshCount = hasTranslucent ? 2 : 1;

            meshData.SetSubMesh(0,
                new SubMeshDescriptor(0, opaqueIndices.Length, MeshTopology.Triangles),
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            if (hasTranslucent)
            {
                meshData.SetSubMesh(1,
                    new SubMeshDescriptor(opaqueIndices.Length, translucentIndices.Length, MeshTopology.Triangles),
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            }

            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, target,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            target.RecalculateBounds();
        }
    }
}
