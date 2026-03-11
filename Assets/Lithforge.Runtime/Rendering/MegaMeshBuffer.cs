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
    /// Owns a single large Mesh containing the merged geometry for one render layer
    /// (opaque, cutout, or translucent). Append-only allocation: new chunks are written
    /// at the end of the buffer. Freed chunks have their indices zeroed (batched per frame)
    /// to produce degenerate triangles. Compaction (TryCompact) is available but never called
    /// from the render path — invoke it only at safe moments (pause, render distance change).
    /// A CPU-side mirror of the vertex and index data enables compaction without GPU readback.
    /// Owner: ChunkMeshStore. Lifetime: application session.
    /// </summary>
    public sealed class MegaMeshBuffer : IDisposable
    {
        private static readonly MeshUpdateFlags _updateFlags =
            MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

        private readonly Mesh _mesh;
        private readonly Dictionary<int3, SlotInfo> _slots = new Dictionary<int3, SlotInfo>();
        private readonly List<SlotInfo> _pendingZeros = new List<SlotInfo>();

        private MeshVertex[] _vertexMirror;
        private int[] _indexMirror;
        private int _vertexCapacity;
        private int _indexCapacity;
        private int _usedVertices;
        private int _usedIndices;
        private int _wastedVertices;
        private int _wastedIndices;
        private bool _subMeshDirty;

        public Mesh Mesh
        {
            get { return _mesh; }
        }

        public bool HasGeometry
        {
            get { return _usedIndices - _wastedIndices > 0; }
        }

        /// <summary>
        /// Creates a MegaMeshBuffer with the given initial capacities.
        /// The buffer grows automatically by doubling when capacity is exceeded.
        /// </summary>
        /// <param name="bufferName">Name for the Mesh object (for debugging).</param>
        /// <param name="initialVertexCapacity">Initial vertex buffer capacity.</param>
        /// <param name="initialIndexCapacity">Initial index buffer capacity.</param>
        public MegaMeshBuffer(string bufferName, int initialVertexCapacity, int initialIndexCapacity)
        {
            _vertexCapacity = initialVertexCapacity;
            _indexCapacity = initialIndexCapacity;
            _vertexMirror = new MeshVertex[initialVertexCapacity];
            _indexMirror = new int[initialIndexCapacity];

            _mesh = new Mesh
            {
                name = bufferName,
            };
            _mesh.MarkDynamic();
            _mesh.SetVertexBufferParams(initialVertexCapacity, MeshVertex.VertexAttributes);
            _mesh.SetIndexBufferParams(initialIndexCapacity, IndexFormat.UInt32);

            _mesh.subMeshCount = 1;
            _mesh.SetSubMesh(0,
                new SubMeshDescriptor(0, 0, MeshTopology.Triangles),
                _updateFlags);

            // Set very large bounds so Unity never frustum-culls the mega-mesh
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f));
        }

        /// <summary>
        /// Writes chunk mesh data into the buffer. If the chunk already has a slot,
        /// its old indices are zeroed and new data is appended at the end.
        /// Vertices are transformed to world-space using the chunk coordinate.
        /// </summary>
        public void AllocateOrUpdate(
            int3 coord,
            NativeList<MeshVertex> vertices,
            NativeList<int> indices)
        {
            int vertCount = vertices.Length;
            int idxCount = indices.Length;

            // Free existing slot if this chunk already has data
            if (_slots.TryGetValue(coord, out SlotInfo existingSlot))
            {
                Array.Clear(_indexMirror, existingSlot.IndexOffset, existingSlot.IndexCount);
                _pendingZeros.Add(existingSlot);
                _wastedVertices += existingSlot.VertexCount;
                _wastedIndices += existingSlot.IndexCount;
                _slots.Remove(coord);
            }

            // Skip empty data
            if (vertCount == 0 || idxCount == 0)
            {
                _subMeshDirty = true;
                return;
            }

            // Ensure capacity (compact or grow if needed)
            EnsureCapacity(vertCount, idxCount);

            // Compute world offset from chunk coordinate
            float3 worldOffset = new float3(
                coord.x * ChunkConstants.Size,
                coord.y * ChunkConstants.Size,
                coord.z * ChunkConstants.Size);

            // Write vertices (world-space transformed) to CPU mirror
            int vOff = _usedVertices;

            for (int i = 0; i < vertCount; i++)
            {
                MeshVertex v = vertices[i];
                v.Position = v.Position + worldOffset;
                _vertexMirror[vOff + i] = v;
            }

            // Write indices (offset-adjusted) to CPU mirror
            int iOff = _usedIndices;

            for (int i = 0; i < idxCount; i++)
            {
                _indexMirror[iOff + i] = indices[i] + vOff;
            }

            // Upload the new ranges to GPU
            _mesh.SetVertexBufferData(_vertexMirror, vOff, vOff, vertCount, 0, _updateFlags);
            _mesh.SetIndexBufferData(_indexMirror, iOff, iOff, idxCount, _updateFlags);

            // Record slot
            _slots[coord] = new SlotInfo
            {
                VertexOffset = vOff,
                VertexCount = vertCount,
                IndexOffset = iOff,
                IndexCount = idxCount,
            };

            _usedVertices += vertCount;
            _usedIndices += idxCount;
            _subMeshDirty = true;
        }

        /// <summary>
        /// Frees the slot occupied by a chunk. Zeroes the index data on the GPU
        /// so that the region renders as degenerate (invisible) triangles.
        /// </summary>
        public void Free(int3 coord)
        {
            if (!_slots.TryGetValue(coord, out SlotInfo slot))
            {
                return;
            }

            // Zero CPU mirror immediately but defer GPU upload to FlushSubMesh
            Array.Clear(_indexMirror, slot.IndexOffset, slot.IndexCount);
            _pendingZeros.Add(slot);

            _wastedVertices += slot.VertexCount;
            _wastedIndices += slot.IndexCount;
            _slots.Remove(coord);
            _subMeshDirty = true;
        }

        /// <summary>
        /// Updates the submesh descriptor if dirty. Must be called before rendering.
        /// </summary>
        public void FlushSubMesh()
        {
            // Batch upload deferred zero ranges from Free() calls
            for (int i = 0; i < _pendingZeros.Count; i++)
            {
                SlotInfo slot = _pendingZeros[i];
                _mesh.SetIndexBufferData(_indexMirror, slot.IndexOffset, slot.IndexOffset,
                    slot.IndexCount, _updateFlags);
            }

            _pendingZeros.Clear();

            if (!_subMeshDirty)
            {
                return;
            }

            _mesh.SetSubMesh(0,
                new SubMeshDescriptor(0, _usedIndices, MeshTopology.Triangles),
                _updateFlags);

            _subMeshDirty = false;
        }

        /// <summary>
        /// Compacts the buffer if waste exceeds 25% of used space.
        /// Should be called periodically (e.g. every few seconds) to reclaim
        /// wasted space from freed chunks, reducing GPU work on degenerate triangles.
        /// </summary>
        public void TryCompact()
        {
            if (_wastedIndices > _usedIndices / 4 || _wastedVertices > _usedVertices / 4)
            {
                Compact();
            }
        }

        public void Dispose()
        {
            if (_mesh != null)
            {
                UnityEngine.Object.Destroy(_mesh);
            }

            _slots.Clear();
            _pendingZeros.Clear();
            _vertexMirror = null;
            _indexMirror = null;
        }

        /// <summary>
        /// Ensures the buffer has room for the given number of additional vertices and indices.
        /// Grows by doubling if capacity is exceeded. Never compacts — compaction is deferred
        /// to explicit TryCompact() calls outside the hot path.
        /// </summary>
        private void EnsureCapacity(int extraVertices, int extraIndices)
        {
            bool vertsFit = _usedVertices + extraVertices <= _vertexCapacity;
            bool idxFit = _usedIndices + extraIndices <= _indexCapacity;

            if (vertsFit && idxFit)
            {
                return;
            }

            Grow(extraVertices, extraIndices);
        }

        /// <summary>
        /// Defragments the buffer by moving all active slots to the front,
        /// eliminating gaps from freed chunks. Requires a full re-upload.
        /// Allocates a temporary sort list (acceptable since compaction is rare).
        /// </summary>
        private void Compact()
        {
            if (_slots.Count == 0)
            {
                _usedVertices = 0;
                _usedIndices = 0;
                _wastedVertices = 0;
                _wastedIndices = 0;
                return;
            }

            // Collect and sort slots by vertex offset for safe in-place compaction.
            // Processing lowest offsets first ensures we only move data leftward,
            // so source data is never overwritten before being read.
            //
            // INVARIANT: IndexOffset ordering matches VertexOffset ordering.
            // This holds because AllocateOrUpdate always appends both vertex and index data
            // atomically in the same call. If this ever changes, the index-move pass must
            // be sorted independently by IndexOffset.
            List<KeyValuePair<int3, SlotInfo>> sortedSlots =
                new List<KeyValuePair<int3, SlotInfo>>(_slots);
            sortedSlots.Sort(CompareSlotsByVertexOffset);

#if UNITY_ASSERTIONS
            for (int s = 1; s < sortedSlots.Count; s++)
            {
                UnityEngine.Debug.Assert(
                    sortedSlots[s].Value.IndexOffset > sortedSlots[s - 1].Value.IndexOffset,
                    "[MegaMeshBuffer] IndexOffset ordering violated — compaction will corrupt data.");
            }
#endif

            int newVertUsed = 0;
            int newIdxUsed = 0;

            for (int s = 0; s < sortedSlots.Count; s++)
            {
                KeyValuePair<int3, SlotInfo> pair = sortedSlots[s];
                SlotInfo slot = pair.Value;
                int3 coord = pair.Key;

                int vertShift = newVertUsed - slot.VertexOffset;

                // Move vertex data (skip if already in position)
                if (slot.VertexOffset != newVertUsed)
                {
                    Array.Copy(_vertexMirror, slot.VertexOffset,
                        _vertexMirror, newVertUsed, slot.VertexCount);
                }

                // Move and adjust index data
                if (vertShift != 0 || slot.IndexOffset != newIdxUsed)
                {
                    for (int i = 0; i < slot.IndexCount; i++)
                    {
                        _indexMirror[newIdxUsed + i] =
                            _indexMirror[slot.IndexOffset + i] + vertShift;
                    }
                }

                // Update slot info in the dictionary
                _slots[coord] = new SlotInfo
                {
                    VertexOffset = newVertUsed,
                    VertexCount = slot.VertexCount,
                    IndexOffset = newIdxUsed,
                    IndexCount = slot.IndexCount,
                };

                newVertUsed += slot.VertexCount;
                newIdxUsed += slot.IndexCount;
            }

            _usedVertices = newVertUsed;
            _usedIndices = newIdxUsed;
            _wastedVertices = 0;
            _wastedIndices = 0;

            // Re-upload the entire active region to the GPU
            if (newVertUsed > 0)
            {
                _mesh.SetVertexBufferData(_vertexMirror, 0, 0, newVertUsed, 0, _updateFlags);
            }

            if (newIdxUsed > 0)
            {
                _mesh.SetIndexBufferData(_indexMirror, 0, 0, newIdxUsed, _updateFlags);
            }

            _subMeshDirty = true;

            UnityEngine.Debug.Log(
                $"[MegaMeshBuffer] {_mesh.name}: compacted to " +
                $"{newVertUsed} vertices, {newIdxUsed} indices");
        }

        /// <summary>
        /// Grows the buffer capacity by doubling (or more if the required extra exceeds a double).
        /// Recreates the GPU buffers and re-uploads all current data.
        /// </summary>
        private void Grow(int extraVertices, int extraIndices)
        {
            int newVertCap = math.max(_vertexCapacity * 2, _usedVertices + extraVertices);
            int newIdxCap = math.max(_indexCapacity * 2, _usedIndices + extraIndices);

            // Resize CPU mirrors
            Array.Resize(ref _vertexMirror, newVertCap);
            Array.Resize(ref _indexMirror, newIdxCap);

            // Recreate GPU buffers (clears existing data)
            _mesh.SetVertexBufferParams(newVertCap, MeshVertex.VertexAttributes);
            _mesh.SetIndexBufferParams(newIdxCap, IndexFormat.UInt32);

            // Re-upload current data to the new GPU buffers
            if (_usedVertices > 0)
            {
                _mesh.SetVertexBufferData(_vertexMirror, 0, 0, _usedVertices, 0, _updateFlags);
            }

            if (_usedIndices > 0)
            {
                _mesh.SetIndexBufferData(_indexMirror, 0, 0, _usedIndices, _updateFlags);
            }

            _vertexCapacity = newVertCap;
            _indexCapacity = newIdxCap;
            _subMeshDirty = true;

            UnityEngine.Debug.Log(
                $"[MegaMeshBuffer] {_mesh.name}: grew to " +
                $"{newVertCap} vertices, {newIdxCap} indices capacity");
        }

        private static int CompareSlotsByVertexOffset(
            KeyValuePair<int3, SlotInfo> a,
            KeyValuePair<int3, SlotInfo> b)
        {
            return a.Value.VertexOffset.CompareTo(b.Value.VertexOffset);
        }

        private struct SlotInfo
        {
            public int VertexOffset;
            public int VertexCount;
            public int IndexOffset;
            public int IndexCount;
        }
    }
}
