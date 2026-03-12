using System;
using System.Collections.Generic;
using Lithforge.Meshing;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    /// Stores chunk meshes in three persistent GPU buffers (opaque, cutout, translucent)
    /// and draws them with exactly 3 RenderPrimitivesIndexedIndirect calls per frame.
    /// No GameObjects, MeshFilters, MeshRenderers, or Mesh objects are created.
    /// Shaders read vertex data from StructuredBuffer via SV_VertexID.
    /// Owner: LithforgeBootstrap. Lifetime: application session.
    /// </summary>
    public sealed class ChunkMeshStore : IDisposable
    {
        private static readonly int _vertexBufferId = Shader.PropertyToID("_VertexBuffer");

        /// <summary>Very large bounds so URP never frustum-culls the procedural draws.</summary>
        private static readonly Bounds _worldBounds =
            new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f));

        private readonly HashSet<int3> _activeChunks = new HashSet<int3>();
        private readonly MegaMeshBuffer _opaqueBuffer;
        private readonly MegaMeshBuffer _cutoutBuffer;
        private readonly MegaMeshBuffer _translucentBuffer;

        private readonly RenderParams _opaqueParams;
        private readonly RenderParams _cutoutParams;
        private readonly RenderParams _translucentParams;

        public int RendererCount
        {
            get { return _activeChunks.Count; }
        }

        public Material OpaqueMaterial { get; }

        public Material CutoutMaterial { get; }

        public Material TranslucentMaterial { get; }

        public ChunkMeshStore(
            Material opaqueMaterial, Material cutoutMaterial, Material translucentMaterial,
            int renderDistance, int yLoadMin, int yLoadMax)
        {
            OpaqueMaterial = opaqueMaterial;
            CutoutMaterial = cutoutMaterial;
            TranslucentMaterial = translucentMaterial;

            // Build RenderParams with MaterialPropertyBlock for buffer binding.
            // The vertex buffer is bound per-frame in RenderAll via matProps.
            _opaqueParams = new RenderParams(opaqueMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
                worldBounds = _worldBounds,
                matProps = new MaterialPropertyBlock(),
            };
            _cutoutParams = new RenderParams(cutoutMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
                worldBounds = _worldBounds,
                matProps = new MaterialPropertyBlock(),
            };
            _translucentParams = new RenderParams(translucentMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
                worldBounds = _worldBounds,
                matProps = new MaterialPropertyBlock(),
            };

            // Estimate buffer sizes from render distance.
            // Average ~3000 opaque verts/chunk, ~4500 indices/chunk.
            // Cutout/translucent are ~10% of opaque.
            // +50% headroom for chunk churn (old slots not yet compacted).
            int diameter = renderDistance * 2 + 1;
            int yLevels = yLoadMax - yLoadMin + 1;
            int maxChunks = diameter * diameter * yLevels;
            int opaqueVerts = maxChunks * 3000 * 3 / 2;
            int opaqueIdx = maxChunks * 4500 * 3 / 2;
            int smallVerts = maxChunks * 300 * 3 / 2;
            int smallIdx = maxChunks * 450 * 3 / 2;

            _opaqueBuffer = new MegaMeshBuffer("MegaMesh_Opaque", opaqueVerts, opaqueIdx);
            _cutoutBuffer = new MegaMeshBuffer("MegaMesh_Cutout", smallVerts, smallIdx);
            _translucentBuffer = new MegaMeshBuffer("MegaMesh_Translucent", smallVerts, smallIdx);
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
            _opaqueBuffer.AllocateOrUpdate(coord, opaqueVerts, opaqueIndices);
            _cutoutBuffer.AllocateOrUpdate(coord, cutoutVerts, cutoutIndices);
            _translucentBuffer.AllocateOrUpdate(coord, translucentVerts, translucentIndices);
            _activeChunks.Add(coord);
        }

        /// <summary>
        /// Updates or creates a chunk's mesh data with a single opaque submesh (LOD).
        /// Called by LODScheduler.PollCompleted.
        /// </summary>
        public void UpdateRendererSingleMesh(
            int3 coord,
            NativeList<MeshVertex> vertices, NativeList<int> indices)
        {
            _opaqueBuffer.AllocateOrUpdate(coord, vertices, indices);
            _cutoutBuffer.Free(coord);
            _translucentBuffer.Free(coord);
            _activeChunks.Add(coord);
        }

        /// <summary>
        /// Submits exactly 3 procedural indexed draw calls (opaque, cutout, translucent).
        /// Each call binds the layer's StructuredBuffer vertex data via MaterialPropertyBlock,
        /// then draws using the hardware index buffer and indirect args from MegaMeshBuffer.
        /// The GPU handles clipping of off-screen triangles. No per-chunk frustum culling
        /// is needed since all geometry is in 3 mega-buffers with huge worldBounds.
        /// Must be called from LateUpdate.
        /// </summary>
        public void RenderAll(Camera camera)
        {
            _opaqueBuffer.FlushArgs();
            _cutoutBuffer.FlushArgs();
            _translucentBuffer.FlushArgs();

            if (_opaqueBuffer.HasGeometry)
            {
                _opaqueParams.matProps.SetBuffer(_vertexBufferId, _opaqueBuffer.VertexBuffer);
                Graphics.RenderPrimitivesIndexedIndirect(
                    _opaqueParams,
                    MeshTopology.Triangles,
                    _opaqueBuffer.IndexBuffer,
                    _opaqueBuffer.ArgsBuffer,
                    1, 0);
            }

            if (_cutoutBuffer.HasGeometry)
            {
                _cutoutParams.matProps.SetBuffer(_vertexBufferId, _cutoutBuffer.VertexBuffer);
                Graphics.RenderPrimitivesIndexedIndirect(
                    _cutoutParams,
                    MeshTopology.Triangles,
                    _cutoutBuffer.IndexBuffer,
                    _cutoutBuffer.ArgsBuffer,
                    1, 0);
            }

            if (_translucentBuffer.HasGeometry)
            {
                _translucentParams.matProps.SetBuffer(_vertexBufferId, _translucentBuffer.VertexBuffer);
                Graphics.RenderPrimitivesIndexedIndirect(
                    _translucentParams,
                    MeshTopology.Triangles,
                    _translucentBuffer.IndexBuffer,
                    _translucentBuffer.ArgsBuffer,
                    1, 0);
            }
        }

        public void DestroyRenderer(int3 coord)
        {
            _opaqueBuffer.Free(coord);
            _cutoutBuffer.Free(coord);
            _translucentBuffer.Free(coord);
            _activeChunks.Remove(coord);
        }

        public void Dispose()
        {
            _activeChunks.Clear();
            _opaqueBuffer.Dispose();
            _cutoutBuffer.Dispose();
            _translucentBuffer.Dispose();
        }
    }
}