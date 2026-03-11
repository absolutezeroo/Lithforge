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
    /// Stores chunk meshes in three mega-mesh buffers (opaque, cutout, translucent)
    /// and draws them with exactly 3 Graphics.RenderMesh calls per frame.
    /// Replaces the old ChunkRenderManager/ChunkRenderer GameObject-based approach.
    /// No GameObjects, MeshFilters, or MeshRenderers are created.
    /// Owner: LithforgeBootstrap. Lifetime: application session.
    /// </summary>
    public sealed class ChunkMeshStore : IDisposable
    {
        /// <summary>Initial vertex capacity for the opaque buffer (most geometry).</summary>
        private const int OpaqueInitialVertices = 131072;
        private const int OpaqueInitialIndices = 196608;

        /// <summary>Cutout and translucent buffers are much smaller.</summary>
        private const int SmallInitialVertices = 16384;
        private const int SmallInitialIndices = 24576;

        private readonly HashSet<int3> _activeChunks = new HashSet<int3>();
        private readonly RenderParams _opaqueParams;
        private readonly RenderParams _cutoutParams;
        private readonly RenderParams _translucentParams;
        private readonly MegaMeshBuffer _opaqueBuffer;
        private readonly MegaMeshBuffer _cutoutBuffer;
        private readonly MegaMeshBuffer _translucentBuffer;

        public int RendererCount
        {
            get { return _activeChunks.Count; }
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

            _opaqueBuffer = new MegaMeshBuffer(
                "MegaMesh_Opaque", OpaqueInitialVertices, OpaqueInitialIndices);
            _cutoutBuffer = new MegaMeshBuffer(
                "MegaMesh_Cutout", SmallInitialVertices, SmallInitialIndices);
            _translucentBuffer = new MegaMeshBuffer(
                "MegaMesh_Translucent", SmallInitialVertices, SmallInitialIndices);
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
        /// Submits exactly 3 draw calls (opaque, cutout, translucent) for the entire world.
        /// The GPU handles clipping of off-screen triangles. No per-chunk frustum culling
        /// is needed since all geometry is in 3 mega-meshes.
        /// Must be called from LateUpdate.
        /// </summary>
        public void RenderAll(Camera camera)
        {
            _opaqueBuffer.FlushSubMesh();
            _cutoutBuffer.FlushSubMesh();
            _translucentBuffer.FlushSubMesh();

            if (_opaqueBuffer.HasGeometry)
            {
                Graphics.RenderMesh(_opaqueParams, _opaqueBuffer.Mesh, 0, Matrix4x4.identity);
            }

            if (_cutoutBuffer.HasGeometry)
            {
                Graphics.RenderMesh(_cutoutParams, _cutoutBuffer.Mesh, 0, Matrix4x4.identity);
            }

            if (_translucentBuffer.HasGeometry)
            {
                Graphics.RenderMesh(_translucentParams, _translucentBuffer.Mesh, 0, Matrix4x4.identity);
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
