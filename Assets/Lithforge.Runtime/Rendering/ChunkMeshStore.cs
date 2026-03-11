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
        /// <summary>Default page count for opaque buffer (most geometry).</summary>
        private const int OpaqueVertexPages = 1024;
        private const int OpaqueIndexPages = 1024;

        /// <summary>Cutout and translucent buffers are much smaller.</summary>
        private const int CutoutVertexPages = 128;
        private const int CutoutIndexPages = 128;
        private const int TranslucentVertexPages = 128;
        private const int TranslucentIndexPages = 128;

        private readonly Dictionary<int3, ChunkSlotEntry> _entries = new Dictionary<int3, ChunkSlotEntry>();
        private readonly RenderParams _opaqueParams;
        private readonly RenderParams _cutoutParams;
        private readonly RenderParams _translucentParams;
        private readonly MegaMeshBuffer _opaqueBuffer;
        private readonly MegaMeshBuffer _cutoutBuffer;
        private readonly MegaMeshBuffer _translucentBuffer;

        public int RendererCount
        {
            get { return _entries.Count; }
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

            _opaqueBuffer = new MegaMeshBuffer("MegaMesh_Opaque", OpaqueVertexPages, OpaqueIndexPages);
            _cutoutBuffer = new MegaMeshBuffer("MegaMesh_Cutout", CutoutVertexPages, CutoutIndexPages);
            _translucentBuffer = new MegaMeshBuffer("MegaMesh_Translucent", TranslucentVertexPages, TranslucentIndexPages);
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
            // Free existing slots if this chunk already has an entry
            FreeExistingSlots(coord);

            ChunkSlotEntry entry = new ChunkSlotEntry();

            // Allocate and write opaque data
            if (opaqueVerts.Length > 0)
            {
                entry.OpaqueSlot = _opaqueBuffer.Allocate(opaqueVerts.Length, opaqueIndices.Length);

                if (entry.OpaqueSlot.IsValid)
                {
                    _opaqueBuffer.WriteSlot(entry.OpaqueSlot, coord, opaqueVerts, opaqueIndices);
                }
            }
            else
            {
                entry.OpaqueSlot = MegaMeshSlot.Invalid;
            }

            // Allocate and write cutout data
            if (cutoutVerts.Length > 0)
            {
                entry.CutoutSlot = _cutoutBuffer.Allocate(cutoutVerts.Length, cutoutIndices.Length);

                if (entry.CutoutSlot.IsValid)
                {
                    _cutoutBuffer.WriteSlot(entry.CutoutSlot, coord, cutoutVerts, cutoutIndices);
                }
            }
            else
            {
                entry.CutoutSlot = MegaMeshSlot.Invalid;
            }

            // Allocate and write translucent data
            if (translucentVerts.Length > 0)
            {
                entry.TranslucentSlot = _translucentBuffer.Allocate(translucentVerts.Length, translucentIndices.Length);

                if (entry.TranslucentSlot.IsValid)
                {
                    _translucentBuffer.WriteSlot(entry.TranslucentSlot, coord, translucentVerts, translucentIndices);
                }
            }
            else
            {
                entry.TranslucentSlot = MegaMeshSlot.Invalid;
            }

            _entries[coord] = entry;
        }

        /// <summary>
        /// Updates or creates a chunk's mesh data with a single opaque submesh (LOD).
        /// Called by LODScheduler.PollCompleted.
        /// </summary>
        public void UpdateRendererSingleMesh(
            int3 coord,
            NativeList<MeshVertex> vertices, NativeList<int> indices)
        {
            // Free existing slots if this chunk already has an entry
            FreeExistingSlots(coord);

            ChunkSlotEntry entry = new ChunkSlotEntry
            {
                CutoutSlot = MegaMeshSlot.Invalid,
                TranslucentSlot = MegaMeshSlot.Invalid,
            };

            if (vertices.Length > 0)
            {
                entry.OpaqueSlot = _opaqueBuffer.Allocate(vertices.Length, indices.Length);

                if (entry.OpaqueSlot.IsValid)
                {
                    _opaqueBuffer.WriteSlot(entry.OpaqueSlot, coord, vertices, indices);
                }
            }
            else
            {
                entry.OpaqueSlot = MegaMeshSlot.Invalid;
            }

            _entries[coord] = entry;
        }

        /// <summary>
        /// Submits exactly 3 draw calls (opaque, cutout, translucent) for the entire world.
        /// The GPU handles clipping of off-screen triangles.
        /// Must be called from LateUpdate.
        /// </summary>
        public void RenderAll()
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
            FreeExistingSlots(coord);
            _entries.Remove(coord);
        }

        public void Dispose()
        {
            _entries.Clear();
            _opaqueBuffer.Dispose();
            _cutoutBuffer.Dispose();
            _translucentBuffer.Dispose();
        }

        private void FreeExistingSlots(int3 coord)
        {
            if (!_entries.TryGetValue(coord, out ChunkSlotEntry existing))
            {
                return;
            }

            if (existing.OpaqueSlot.IsValid)
            {
                _opaqueBuffer.Free(existing.OpaqueSlot);
            }

            if (existing.CutoutSlot.IsValid)
            {
                _cutoutBuffer.Free(existing.CutoutSlot);
            }

            if (existing.TranslucentSlot.IsValid)
            {
                _translucentBuffer.Free(existing.TranslucentSlot);
            }
        }

        private struct ChunkSlotEntry
        {
            public MegaMeshSlot OpaqueSlot;
            public MegaMeshSlot CutoutSlot;
            public MegaMeshSlot TranslucentSlot;
        }
    }
}
