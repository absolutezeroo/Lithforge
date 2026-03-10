using System;
using System.Collections.Generic;
using Lithforge.Meshing;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime.Rendering
{
    public sealed class ChunkRenderManager : IDisposable
    {
        private readonly Dictionary<int3, ChunkRenderer> _renderers = new Dictionary<int3, ChunkRenderer>();
        private readonly Material _opaqueMaterial;
        private readonly Material _cutoutMaterial;
        private readonly Material _translucentMaterial;
        private readonly Material[] _materials;
        private readonly Transform _parent;

        public int RendererCount
        {
            get { return _renderers.Count; }
        }

        public Material OpaqueMaterial
        {
            get { return _opaqueMaterial; }
        }

        public Material CutoutMaterial
        {
            get { return _cutoutMaterial; }
        }

        public Material TranslucentMaterial
        {
            get { return _translucentMaterial; }
        }

        public ChunkRenderManager(Material opaqueMaterial, Material cutoutMaterial, Material translucentMaterial)
        {
            _opaqueMaterial = opaqueMaterial;
            _cutoutMaterial = cutoutMaterial;
            _translucentMaterial = translucentMaterial;
            _materials = new Material[] { opaqueMaterial, cutoutMaterial, translucentMaterial };

            GameObject parentGo = new GameObject("ChunkRenderers");
            _parent = parentGo.transform;
        }

        public void UpdateRenderer(
            int3 coord,
            NativeList<MeshVertex> opaqueVerts, NativeList<int> opaqueIndices,
            NativeList<MeshVertex> translucentVerts, NativeList<int> translucentIndices)
        {
            ChunkRenderer renderer = GetOrCreateRenderer(coord);
            renderer.UpdateMesh(opaqueVerts, opaqueIndices, translucentVerts, translucentIndices);
        }

        public void UpdateRenderer(
            int3 coord,
            NativeList<MeshVertex> opaqueVerts, NativeList<int> opaqueIndices,
            NativeList<MeshVertex> cutoutVerts, NativeList<int> cutoutIndices,
            NativeList<MeshVertex> translucentVerts, NativeList<int> translucentIndices)
        {
            ChunkRenderer renderer = GetOrCreateRenderer(coord);
            renderer.UpdateMesh(opaqueVerts, opaqueIndices, cutoutVerts, cutoutIndices, translucentVerts, translucentIndices);
        }

        /// <summary>
        /// Updates a chunk renderer with a single-submesh opaque mesh (used for LOD).
        /// </summary>
        public void UpdateRendererSingleMesh(
            int3 coord,
            NativeList<MeshVertex> vertices, NativeList<int> indices)
        {
            ChunkRenderer renderer = GetOrCreateRenderer(coord);
            renderer.UpdateMesh(vertices, indices);
        }

        private ChunkRenderer GetOrCreateRenderer(int3 coord)
        {
            if (!_renderers.TryGetValue(coord, out ChunkRenderer renderer))
            {
                GameObject go = new GameObject($"Chunk_{coord.x}_{coord.y}_{coord.z}");
                go.transform.SetParent(_parent, false);
                renderer = go.AddComponent<ChunkRenderer>();
                renderer.Initialize(coord, _materials);
                _renderers[coord] = renderer;
            }

            return renderer;
        }

        public void DestroyRenderer(int3 coord)
        {
            if (_renderers.TryGetValue(coord, out ChunkRenderer renderer))
            {
                _renderers.Remove(coord);

                if (renderer != null && renderer.gameObject != null)
                {
                    UnityEngine.Object.Destroy(renderer.gameObject);
                }
            }
        }

        public void Dispose()
        {
            List<int3> coords = new List<int3>(_renderers.Keys);

            for (int i = 0; i < coords.Count; i++)
            {
                DestroyRenderer(coords[i]);
            }

            if (_parent != null)
            {
                UnityEngine.Object.Destroy(_parent.gameObject);
            }
        }
    }
}
