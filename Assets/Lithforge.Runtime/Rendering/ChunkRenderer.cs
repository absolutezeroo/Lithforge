using Lithforge.Meshing;
using Lithforge.Voxel.Chunk;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime.Rendering
{
    public sealed class ChunkRenderer : MonoBehaviour
    {
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;

        public void Initialize(int3 chunkCoord, Material material)
        {
            _meshFilter = gameObject.AddComponent<MeshFilter>();
            _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _meshRenderer.sharedMaterial = material;
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;

            _mesh = new Mesh
            {
                name = $"Chunk_{chunkCoord.x}_{chunkCoord.y}_{chunkCoord.z}",
            };
            _meshFilter.sharedMesh = _mesh;

            Vector3 worldPos = new Vector3(
                chunkCoord.x * ChunkConstants.Size,
                chunkCoord.y * ChunkConstants.Size,
                chunkCoord.z * ChunkConstants.Size);

            transform.position = worldPos;
        }

        public void UpdateMesh(NativeList<MeshVertex> verts, NativeList<int> indices)
        {
            MeshUploader.Upload(_mesh, verts, indices);
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }
    }
}
