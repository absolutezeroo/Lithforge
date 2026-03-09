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
        private readonly Transform _parent;

        public int RendererCount
        {
            get { return _renderers.Count; }
        }

        public Material Material
        {
            get { return _opaqueMaterial; }
        }

        public ChunkRenderManager(Material opaqueMaterial)
        {
            _opaqueMaterial = opaqueMaterial;

            GameObject parentGo = new GameObject("ChunkRenderers");
            _parent = parentGo.transform;
        }

        public void UpdateRenderer(int3 coord, NativeList<MeshVertex> verts, NativeList<int> indices)
        {

            if (!_renderers.TryGetValue(coord, out ChunkRenderer renderer))
            {
                GameObject go = new GameObject($"Chunk_{coord.x}_{coord.y}_{coord.z}");
                go.transform.SetParent(_parent, false);
                renderer = go.AddComponent<ChunkRenderer>();
                renderer.Initialize(coord, _opaqueMaterial);

                _renderers[coord] = renderer;
            }

            renderer.UpdateMesh(verts, indices);
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
