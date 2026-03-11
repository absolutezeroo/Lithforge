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
    /// (opaque, cutout, or translucent). Freed slots are recycled via a coalescing
    /// free-list so the buffer stays bounded during player movement. When a chunk is
    /// re-meshed and the new data fits in the existing slot, it is rewritten in-place
    /// with no free-list interaction. Indices of freed regions are zeroed (batched per
    /// frame) to produce degenerate triangles until the slot is reclaimed.
    /// A CPU-side mirror of the vertex and index data avoids GPU readback.
    /// Owner: ChunkMeshStore. Lifetime: application session.
    /// </summary>
    public sealed class MegaMeshBuffer : IDisposable
    {
        private static readonly MeshUpdateFlags _updateFlags =
            MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

        private readonly Mesh _mesh;
        private readonly Dictionary<int3, SlotInfo> _slots = new Dictionary<int3, SlotInfo>();

        /// <summary>Free regions sorted by VertexOffset for coalescing and first-fit search.</summary>
        private readonly List<FreeRegion> _freeRegions = new List<FreeRegion>();
        private readonly List<PendingZero> _pendingZeros = new List<PendingZero>();

        private MeshVertex[] _vertexMirror;
        private int[] _indexMirror;
        private int _vertexCapacity;
        private int _indexCapacity;
        private int _usedVertices;
        private int _usedIndices;
        private bool _subMeshDirty;

        public Mesh Mesh
        {
            get { return _mesh; }
        }

        public bool HasGeometry
        {
            get { return _slots.Count > 0; }
        }

        /// <summary>
        /// Creates a MegaMeshBuffer with the given initial capacities.
        /// The buffer grows automatically by doubling when capacity is exceeded.
        /// </summary>
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
        /// Writes chunk mesh data into the buffer. Three paths:
        /// 1. Existing slot fits new data → rewrite in-place (fastest).
        /// 2. Free region found → reuse recycled space (no buffer growth).
        /// 3. Neither → append at end (may trigger Grow).
        /// Vertices are transformed to world-space using the chunk coordinate.
        /// </summary>
        public void AllocateOrUpdate(
            int3 coord,
            NativeList<MeshVertex> vertices,
            NativeList<int> indices)
        {
            int vertCount = vertices.Length;
            int idxCount = indices.Length;

            // Path 1: existing slot — try in-place reuse
            if (_slots.TryGetValue(coord, out SlotInfo existing))
            {
                if (vertCount <= existing.VertexCapacity && idxCount <= existing.IndexCapacity)
                {
                    if (vertCount == 0 || idxCount == 0)
                    {
                        FreeSlot(coord, existing);
                        _subMeshDirty = true;
                        return;
                    }

                    WriteData(existing.VertexOffset, existing.IndexOffset, coord, vertices, indices);

                    // Zero leftover indices if new data is smaller
                    int leftoverIdx = existing.IndexCount - idxCount;

                    if (leftoverIdx > 0)
                    {
                        int clearOffset = existing.IndexOffset + idxCount;
                        Array.Clear(_indexMirror, clearOffset, leftoverIdx);
                        _pendingZeros.Add(new PendingZero
                        {
                            IndexOffset = clearOffset,
                            IndexCount = leftoverIdx,
                        });
                    }

                    _slots[coord] = new SlotInfo
                    {
                        VertexOffset = existing.VertexOffset,
                        VertexCount = vertCount,
                        VertexCapacity = existing.VertexCapacity,
                        IndexOffset = existing.IndexOffset,
                        IndexCount = idxCount,
                        IndexCapacity = existing.IndexCapacity,
                    };
                    _subMeshDirty = true;
                    return;
                }

                // Doesn't fit — free and allocate new
                FreeSlot(coord, existing);
            }

            if (vertCount == 0 || idxCount == 0)
            {
                _subMeshDirty = true;
                return;
            }

            // Path 2: try to find a free region (first-fit)
            int regionIdx = FindFreeRegion(vertCount, idxCount);
            int vOff;
            int iOff;
            int vCap;
            int iCap;

            if (regionIdx >= 0)
            {
                FreeRegion region = _freeRegions[regionIdx];
                vOff = region.VertexOffset;
                iOff = region.IndexOffset;
                vCap = region.VertexCapacity;
                iCap = region.IndexCapacity;

                // Split if remainder is meaningful (> 256 verts / 384 indices)
                int remainVerts = vCap - vertCount;
                int remainIdx = iCap - idxCount;

                if (remainVerts > 256 && remainIdx > 384)
                {
                    _freeRegions[regionIdx] = new FreeRegion
                    {
                        VertexOffset = vOff + vertCount,
                        VertexCapacity = remainVerts,
                        IndexOffset = iOff + idxCount,
                        IndexCapacity = remainIdx,
                    };
                    vCap = vertCount;
                    iCap = idxCount;
                }
                else
                {
                    _freeRegions.RemoveAt(regionIdx);
                }
            }
            else
            {
                // Path 3: append at end
                EnsureCapacity(vertCount, idxCount);
                vOff = _usedVertices;
                iOff = _usedIndices;
                vCap = vertCount;
                iCap = idxCount;
                _usedVertices += vertCount;
                _usedIndices += idxCount;
            }

            WriteData(vOff, iOff, coord, vertices, indices);

            _slots[coord] = new SlotInfo
            {
                VertexOffset = vOff,
                VertexCount = vertCount,
                VertexCapacity = vCap,
                IndexOffset = iOff,
                IndexCount = idxCount,
                IndexCapacity = iCap,
            };
            _subMeshDirty = true;
        }

        /// <summary>
        /// Frees the slot occupied by a chunk. Zeroes the index data in the CPU mirror
        /// (GPU upload deferred to FlushSubMesh) and returns the region to the free-list.
        /// </summary>
        public void Free(int3 coord)
        {
            if (!_slots.TryGetValue(coord, out SlotInfo slot))
            {
                return;
            }

            FreeSlot(coord, slot);
            _subMeshDirty = true;
        }

        /// <summary>
        /// Uploads deferred index-zero ranges and updates the submesh descriptor.
        /// Must be called before rendering.
        /// </summary>
        public void FlushSubMesh()
        {
            // Batch upload deferred zero ranges
            for (int i = 0; i < _pendingZeros.Count; i++)
            {
                PendingZero pz = _pendingZeros[i];
                _mesh.SetIndexBufferData(_indexMirror, pz.IndexOffset, pz.IndexOffset,
                    pz.IndexCount, _updateFlags);
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

        public void Dispose()
        {
            if (_mesh != null)
            {
                UnityEngine.Object.Destroy(_mesh);
            }

            _slots.Clear();
            _freeRegions.Clear();
            _pendingZeros.Clear();
            _vertexMirror = null;
            _indexMirror = null;
        }

        /// <summary>
        /// Transforms vertices to world-space and writes vertex + index data to the
        /// CPU mirror and GPU buffer at the given offsets.
        /// </summary>
        private void WriteData(
            int vOff, int iOff, int3 coord,
            NativeList<MeshVertex> vertices, NativeList<int> indices)
        {
            int vertCount = vertices.Length;
            int idxCount = indices.Length;

            float3 worldOffset = new float3(
                coord.x * ChunkConstants.Size,
                coord.y * ChunkConstants.Size,
                coord.z * ChunkConstants.Size);

            for (int i = 0; i < vertCount; i++)
            {
                MeshVertex v = vertices[i];
                v.Position = v.Position + worldOffset;
                _vertexMirror[vOff + i] = v;
            }

            for (int i = 0; i < idxCount; i++)
            {
                _indexMirror[iOff + i] = indices[i] + vOff;
            }

            _mesh.SetVertexBufferData(_vertexMirror, vOff, vOff, vertCount, 0, _updateFlags);
            _mesh.SetIndexBufferData(_indexMirror, iOff, iOff, idxCount, _updateFlags);
        }

        /// <summary>
        /// Removes a slot from the active dictionary, zeroes its indices in the CPU mirror,
        /// and returns the region to the free-list with coalescing and tail reclaim.
        /// </summary>
        private void FreeSlot(int3 coord, SlotInfo slot)
        {
            // Zero indices in CPU mirror, defer GPU upload
            Array.Clear(_indexMirror, slot.IndexOffset, slot.IndexCount);
            _pendingZeros.Add(new PendingZero
            {
                IndexOffset = slot.IndexOffset,
                IndexCount = slot.IndexCount,
            });
            _slots.Remove(coord);

            // Insert into free-list sorted by VertexOffset
            FreeRegion region = new FreeRegion
            {
                VertexOffset = slot.VertexOffset,
                VertexCapacity = slot.VertexCapacity,
                IndexOffset = slot.IndexOffset,
                IndexCapacity = slot.IndexCapacity,
            };

            int insertIdx = 0;

            for (int i = 0; i < _freeRegions.Count; i++)
            {
                if (_freeRegions[i].VertexOffset > region.VertexOffset)
                {
                    break;
                }

                insertIdx = i + 1;
            }

            _freeRegions.Insert(insertIdx, region);

            // Coalesce with adjacent free regions
            CoalesceAt(insertIdx);

            // Reclaim tail: if the last free region extends to _usedVertices, shrink
            if (_freeRegions.Count > 0)
            {
                FreeRegion last = _freeRegions[_freeRegions.Count - 1];

                if (last.VertexOffset + last.VertexCapacity >= _usedVertices)
                {
                    _usedVertices = last.VertexOffset;
                    _usedIndices = last.IndexOffset;
                    _freeRegions.RemoveAt(_freeRegions.Count - 1);
                    _subMeshDirty = true;
                }
            }
        }

        /// <summary>
        /// Merges the free region at the given index with its immediate neighbors
        /// if they are contiguous in both vertex and index space.
        /// </summary>
        private void CoalesceAt(int idx)
        {
            // Merge with next
            if (idx + 1 < _freeRegions.Count)
            {
                FreeRegion current = _freeRegions[idx];
                FreeRegion next = _freeRegions[idx + 1];

                if (current.VertexOffset + current.VertexCapacity == next.VertexOffset)
                {
                    _freeRegions[idx] = new FreeRegion
                    {
                        VertexOffset = current.VertexOffset,
                        VertexCapacity = current.VertexCapacity + next.VertexCapacity,
                        IndexOffset = current.IndexOffset,
                        IndexCapacity = current.IndexCapacity + next.IndexCapacity,
                    };
                    _freeRegions.RemoveAt(idx + 1);
                }
            }

            // Merge with previous
            if (idx > 0)
            {
                FreeRegion prev = _freeRegions[idx - 1];
                FreeRegion current = _freeRegions[idx];

                if (prev.VertexOffset + prev.VertexCapacity == current.VertexOffset)
                {
                    _freeRegions[idx - 1] = new FreeRegion
                    {
                        VertexOffset = prev.VertexOffset,
                        VertexCapacity = prev.VertexCapacity + current.VertexCapacity,
                        IndexOffset = prev.IndexOffset,
                        IndexCapacity = prev.IndexCapacity + current.IndexCapacity,
                    };
                    _freeRegions.RemoveAt(idx);
                }
            }
        }

        /// <summary>
        /// Searches the free-list for the first region that can hold the requested
        /// vertex and index counts. Returns the index into _freeRegions, or -1.
        /// </summary>
        private int FindFreeRegion(int vertCount, int idxCount)
        {
            for (int i = 0; i < _freeRegions.Count; i++)
            {
                FreeRegion region = _freeRegions[i];

                if (region.VertexCapacity >= vertCount && region.IndexCapacity >= idxCount)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Ensures the buffer has room for the given number of additional vertices and indices
        /// at the append position. Grows by doubling if capacity is exceeded.
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
        /// Grows the buffer capacity by doubling (or more if the required extra exceeds a double).
        /// Recreates the GPU buffers and re-uploads all current data.
        /// </summary>
        private void Grow(int extraVertices, int extraIndices)
        {
            int newVertCap = math.max(_vertexCapacity * 2, _usedVertices + extraVertices);
            int newIdxCap = math.max(_indexCapacity * 2, _usedIndices + extraIndices);

            Array.Resize(ref _vertexMirror, newVertCap);
            Array.Resize(ref _indexMirror, newIdxCap);

            _mesh.SetVertexBufferParams(newVertCap, MeshVertex.VertexAttributes);
            _mesh.SetIndexBufferParams(newIdxCap, IndexFormat.UInt32);

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

        private struct SlotInfo
        {
            public int VertexOffset;
            public int VertexCount;
            public int VertexCapacity;
            public int IndexOffset;
            public int IndexCount;
            public int IndexCapacity;
        }

        private struct FreeRegion
        {
            public int VertexOffset;
            public int VertexCapacity;
            public int IndexOffset;
            public int IndexCapacity;
        }

        private struct PendingZero
        {
            public int IndexOffset;
            public int IndexCount;
        }
    }
}
