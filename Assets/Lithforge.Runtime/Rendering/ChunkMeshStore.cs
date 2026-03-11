using System;
using System.Collections.Generic;
using Lithforge.Meshing;
using Lithforge.Voxel.Chunk;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    /// Stores per-chunk Mesh objects and draws them each frame with Graphics.RenderMesh.
    /// Replaces the old ChunkRenderManager/ChunkRenderer GameObject-based approach.
    /// No GameObjects, MeshFilters, or MeshRenderers are created.
    /// Owner: LithforgeBootstrap. Lifetime: application session.
    /// </summary>
    public sealed class ChunkMeshStore : IDisposable
    {
        private readonly Dictionary<int3, ChunkMeshEntry> _meshes = new Dictionary<int3, ChunkMeshEntry>();
        private readonly RenderParams _opaqueParams;
        private readonly RenderParams _cutoutParams;
        private readonly RenderParams _translucentParams;
        private readonly Plane[] _frustumPlanes = new Plane[6];

        public int RendererCount
        {
            get { return _meshes.Count; }
        }

        public Material OpaqueMaterial { get; }

        public Material CutoutMaterial { get; }

        public Material TranslucentMaterial { get; }

        public ChunkMeshStore(Material opaqueMaterial, Material cutoutMaterial, Material translucentMaterial)
        {
            OpaqueMaterial = opaqueMaterial;
            CutoutMaterial = cutoutMaterial;
            TranslucentMaterial = translucentMaterial;

            _opaqueParams = new RenderParams(opaqueMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
            };
            _cutoutParams = new RenderParams(cutoutMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
            };
            _translucentParams = new RenderParams(translucentMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
            };
        }

        /// <summary>
        /// Updates or creates a chunk's mesh data with 3-submesh data (LOD0).
        /// Called by MeshScheduler.PollCompleted.
        /// </summary>
        public void UpdateRenderer(
            int3 coord,
            NativeList<MeshVertex> opaqueVerts, NativeList<int> opaqueIndices,
            NativeList<MeshVertex> cutoutVerts, NativeList<int> cutoutIndices,
            NativeList<MeshVertex> translucentVerts, NativeList<int> translucentIndices)
        {
            Mesh mesh = GetOrCreateMesh(coord);
            MeshUploader.Upload(mesh, opaqueVerts, opaqueIndices,
                cutoutVerts, cutoutIndices,
                translucentVerts, translucentIndices);
            StoreEntry(coord, mesh);
        }

        /// <summary>
        /// Updates or creates a chunk's mesh data with a single opaque submesh (LOD).
        /// Called by LODScheduler.PollCompleted.
        /// </summary>
        public void UpdateRendererSingleMesh(
            int3 coord,
            NativeList<MeshVertex> vertices, NativeList<int> indices)
        {
            Mesh mesh = GetOrCreateMesh(coord);
            MeshUploader.Upload(mesh, vertices, indices);
            StoreEntry(coord, mesh);
        }

        /// <summary>
        /// Draws all chunk meshes visible in the camera frustum.
        /// Submits one Graphics.RenderMesh call per non-empty submesh per visible chunk.
        /// Must be called from LateUpdate.
        /// </summary>
        public void RenderAll(Camera camera)
        {
            if (camera != null)
            {
                GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
            }

            foreach (KeyValuePair<int3, ChunkMeshEntry> pair in _meshes)
            {
                ChunkMeshEntry entry = pair.Value;
                Mesh mesh = entry.Mesh;
                int subMeshCount = mesh.subMeshCount;

                if (subMeshCount == 0)
                {
                    continue;
                }

                // Frustum cull
                if (camera != null && !GeometryUtility.TestPlanesAABB(_frustumPlanes, entry.WorldBounds))
                {
                    continue;
                }

                Matrix4x4 matrix = Matrix4x4.Translate(entry.WorldPosition);

                // Draw opaque submesh
                if (mesh.GetIndexCount(0) > 0)
                {
                    Graphics.RenderMesh(_opaqueParams, mesh, 0, matrix);
                }

                // Draw cutout submesh
                if (subMeshCount >= 2 && mesh.GetIndexCount(1) > 0)
                {
                    Graphics.RenderMesh(_cutoutParams, mesh, 1, matrix);
                }

                // Draw translucent submesh
                if (subMeshCount >= 3 && mesh.GetIndexCount(2) > 0)
                {
                    Graphics.RenderMesh(_translucentParams, mesh, 2, matrix);
                }
            }
        }

        public void DestroyRenderer(int3 coord)
        {
            if (_meshes.TryGetValue(coord, out ChunkMeshEntry entry))
            {
                UnityEngine.Object.Destroy(entry.Mesh);
                _meshes.Remove(coord);
            }
        }

        public void Dispose()
        {
            foreach (KeyValuePair<int3, ChunkMeshEntry> pair in _meshes)
            {
                if (pair.Value.Mesh != null)
                {
                    UnityEngine.Object.Destroy(pair.Value.Mesh);
                }
            }

            _meshes.Clear();
        }

        private Mesh GetOrCreateMesh(int3 coord)
        {
            if (_meshes.TryGetValue(coord, out ChunkMeshEntry existing))
            {
                return existing.Mesh;
            }

            Mesh mesh = new Mesh
            {
                name = $"Chunk_{coord.x}_{coord.y}_{coord.z}",
            };
            mesh.MarkDynamic();
            return mesh;
        }

        private void StoreEntry(int3 coord, Mesh mesh)
        {
            int chunkSize = ChunkConstants.Size;
            Vector3 worldPos = new Vector3(
                coord.x * chunkSize,
                coord.y * chunkSize,
                coord.z * chunkSize);

            Bounds bounds = new Bounds(
                worldPos + new Vector3(chunkSize * 0.5f, chunkSize * 0.5f, chunkSize * 0.5f),
                new Vector3(chunkSize, chunkSize, chunkSize));

            _meshes[coord] = new ChunkMeshEntry
            {
                Mesh = mesh,
                WorldBounds = bounds,
                WorldPosition = worldPos,
            };
        }

        private struct ChunkMeshEntry
        {
            public Mesh Mesh;
            public Bounds WorldBounds;
            public Vector3 WorldPosition;
        }
    }
}
