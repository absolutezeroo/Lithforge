using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Lithforge.Meshing;
using Lithforge.Runtime.Debug;
using Lithforge.Voxel.Chunk;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    /// Owns persistent GPU buffers (vertex + index + indirect args) for one render layer
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
        private static readonly int _vertexStride = Marshal.SizeOf<MeshVertex>();

        private readonly string _name;
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
        private bool _argsDirty;

        private GraphicsBuffer _vertexBuffer;
        private GraphicsBuffer _indexBuffer;
        private GraphicsBuffer _argsBuffer;

        /// <summary>
        /// Cached single-element array for indirect args upload, avoiding per-frame allocation.
        /// </summary>
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _argsUploadBuffer =
            new GraphicsBuffer.IndirectDrawIndexedArgs[1];

        public GraphicsBuffer VertexBuffer
        {
            get { return _vertexBuffer; }
        }

        public GraphicsBuffer IndexBuffer
        {
            get { return _indexBuffer; }
        }

        public GraphicsBuffer ArgsBuffer
        {
            get { return _argsBuffer; }
        }

        public bool HasGeometry
        {
            get { return _slots.Count > 0; }
        }

        public int UsedVertices
        {
            get { return _usedVertices; }
        }

        public int VertexCapacity
        {
            get { return _vertexCapacity; }
        }

        public int FreeRegionCount
        {
            get { return _freeRegions.Count; }
        }

        /// <summary>
        /// Creates a MegaMeshBuffer with the given initial capacities.
        /// The buffer grows automatically by doubling when capacity is exceeded.
        /// </summary>
        public MegaMeshBuffer(string bufferName, int initialVertexCapacity, int initialIndexCapacity)
        {
            _name = bufferName;
            _vertexCapacity = initialVertexCapacity;
            _indexCapacity = initialIndexCapacity;
            _vertexMirror = new MeshVertex[initialVertexCapacity];
            _indexMirror = new int[initialIndexCapacity];

            _vertexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                initialVertexCapacity,
                _vertexStride);

            _indexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Index | GraphicsBuffer.Target.Raw,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                initialIndexCapacity,
                sizeof(int));

            _argsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);

            // Initialize indirect args to zero draws
            _argsUploadBuffer[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = 0,
                instanceCount = 1,
                startIndex = 0,
                baseVertexIndex = 0,
                startInstance = 0,
            };
            _argsBuffer.SetData(_argsUploadBuffer);

            _argsDirty = false;
        }

        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
            _argsBuffer?.Dispose();

            _vertexBuffer = null;
            _indexBuffer = null;
            _argsBuffer = null;

            _slots.Clear();
            _freeRegions.Clear();
            _pendingZeros.Clear();
            _vertexMirror = null;
            _indexMirror = null;
        }

        /// <summary>
        /// Allocates a new slot or updates an existing one for the given chunk coordinate.
        /// Three allocation paths in priority order:
        /// 1. In-place reuse — if the chunk already has a slot and the new data fits, overwrite it.
        /// 2. Free-list first-fit — search the coalescing free-list for a region that fits.
        /// 3. Append — add at the end, growing the buffer if necessary.
        /// Empty meshes (zero vertices) free any existing slot and return immediately.
        /// </summary>
        public void AllocateOrUpdate(
            int3 coord,
            NativeList<MeshVertex> vertices, NativeList<int> indices)
        {
            int vertCount = vertices.Length;
            int idxCount = indices.Length;

            // Empty mesh — free existing slot if any, nothing to draw
            if (vertCount == 0)
            {
                if (_slots.TryGetValue(coord, out SlotInfo existing))
                {
                    FreeSlot(coord, existing);
                    _argsDirty = true;
                }

                return;
            }

            // Path 1: In-place reuse — chunk already has a slot and new data fits
            if (_slots.TryGetValue(coord, out SlotInfo slot))
            {
                if (vertCount <= slot.VertexCapacity && idxCount <= slot.IndexCapacity)
                {
                    // Clear old index tail if the new mesh is shorter
                    if (idxCount < slot.IndexCount)
                    {
                        int tailOffset = slot.IndexOffset + idxCount;
                        int tailCount = slot.IndexCount - idxCount;
                        Array.Clear(_indexMirror, tailOffset, tailCount);

                        NativeArray<int> gpuTail = _indexBuffer.LockBufferForWrite<int>(tailOffset, tailCount);

                        for (int j = 0; j < tailCount; j++)
                        {
                            gpuTail[j] = 0;
                        }

                        _indexBuffer.UnlockBufferAfterWrite<int>(tailCount);
                    }

                    WriteData(slot.VertexOffset, slot.IndexOffset, coord, vertices, indices);

                    _slots[coord] = new SlotInfo
                    {
                        VertexOffset = slot.VertexOffset,
                        VertexCount = vertCount,
                        VertexCapacity = slot.VertexCapacity,
                        IndexOffset = slot.IndexOffset,
                        IndexCount = idxCount,
                        IndexCapacity = slot.IndexCapacity,
                    };

                    _argsDirty = true;
                    return;
                }

                // Old slot too small — free it and fall through to allocate a new one
                FreeSlot(coord, slot);
            }

            // Path 2: Free-list first-fit
            int freeIdx = FindFreeRegion(vertCount, idxCount);

            if (freeIdx >= 0)
            {
                FreeRegion region = _freeRegions[freeIdx];
                _freeRegions.RemoveAt(freeIdx);

                WriteData(region.VertexOffset, region.IndexOffset, coord, vertices, indices);

                // Split remainder back into free-list if significant leftover
                int leftoverVerts = region.VertexCapacity - vertCount;
                int leftoverIdx = region.IndexCapacity - idxCount;

                if (leftoverVerts > 0 && leftoverIdx > 0)
                {
                    FreeRegion remainder = new FreeRegion
                    {
                        VertexOffset = region.VertexOffset + vertCount,
                        VertexCapacity = leftoverVerts,
                        IndexOffset = region.IndexOffset + idxCount,
                        IndexCapacity = leftoverIdx,
                    };

                    // Insert sorted by VertexOffset
                    int insertIdx = 0;

                    for (int i = 0; i < _freeRegions.Count; i++)
                    {
                        if (_freeRegions[i].VertexOffset > remainder.VertexOffset)
                        {
                            break;
                        }

                        insertIdx = i + 1;
                    }

                    _freeRegions.Insert(insertIdx, remainder);
                }

                _slots[coord] = new SlotInfo
                {
                    VertexOffset = region.VertexOffset,
                    VertexCount = vertCount,
                    VertexCapacity = vertCount,
                    IndexOffset = region.IndexOffset,
                    IndexCount = idxCount,
                    IndexCapacity = idxCount,
                };

                _argsDirty = true;
                return;
            }

            // Path 3: Append at end
            EnsureCapacity(vertCount, idxCount);

            int vOff = _usedVertices;
            int iOff = _usedIndices;

            WriteData(vOff, iOff, coord, vertices, indices);

            _slots[coord] = new SlotInfo
            {
                VertexOffset = vOff,
                VertexCount = vertCount,
                VertexCapacity = vertCount,
                IndexOffset = iOff,
                IndexCount = idxCount,
                IndexCapacity = idxCount,
            };

            _usedVertices += vertCount;
            _usedIndices += idxCount;
            _argsDirty = true;
        }

        /// <summary>
        /// Frees the slot for the given chunk coordinate, returning it to the free-list.
        /// No-op if the chunk has no slot.
        /// </summary>
        public void Free(int3 coord)
        {
            if (_slots.TryGetValue(coord, out SlotInfo slot))
            {
                FreeSlot(coord, slot);
                _argsDirty = true;
            }
        }

        /// <summary>
        /// Uploads any pending index zeros to the GPU and updates the indirect args buffer
        /// if dirty. Call once per frame before issuing draw calls.
        /// </summary>
        public void FlushArgs()
        {
            // Upload pending index zeros in a single Lock/Unlock covering the full range.
            // The mirror already has zeros (applied by FreeSlot) and valid data between gaps,
            // so bulk-copying the entire [min..max) range rewrites everything correctly.
            if (_pendingZeros.Count > 0)
            {
                int minOffset = _pendingZeros[0].IndexOffset;
                int maxEnd = _pendingZeros[0].IndexOffset + _pendingZeros[0].IndexCount;

                for (int i = 1; i < _pendingZeros.Count; i++)
                {
                    PendingZero pz = _pendingZeros[i];
                    int pzEnd = pz.IndexOffset + pz.IndexCount;

                    if (pz.IndexOffset < minOffset)
                    {
                        minOffset = pz.IndexOffset;
                    }

                    if (pzEnd > maxEnd)
                    {
                        maxEnd = pzEnd;
                    }
                }

                int rangeLength = maxEnd - minOffset;
                NativeArray<int> gpuIdx = _indexBuffer.LockBufferForWrite<int>(minOffset, rangeLength);

                unsafe
                {
                    void* dstPtr = NativeArrayUnsafeUtility.GetUnsafePtr(gpuIdx);

                    fixed (int* srcPtr = &_indexMirror[minOffset])
                    {
                        UnsafeUtility.MemCpy(dstPtr, srcPtr, (long)rangeLength * sizeof(int));
                    }
                }

                _indexBuffer.UnlockBufferAfterWrite<int>(rangeLength);
                _pendingZeros.Clear();
            }

            // Update indirect args if anything changed
            if (_argsDirty)
            {
                _argsUploadBuffer[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = (uint)_usedIndices,
                    instanceCount = 1,
                    startIndex = 0,
                    baseVertexIndex = 0,
                    startInstance = 0,
                };
                _argsBuffer.SetData(_argsUploadBuffer);
                _argsDirty = false;
            }
        }

        /// <summary>
        /// Transforms vertices to world-space and writes vertex + index data to the
        /// CPU mirror and GPU buffer at the given offsets. Uses LockBufferForWrite
        /// to write directly to GPU-mapped memory, avoiding the staging buffer copy
        /// that SetData would require.
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

            // Write world-offset vertices to GPU and CPU mirror in one loop (was two separate loops).
            // Cannot bulk-memcpy because the world offset transform requires per-element work.
            // Cannot read back from GPU NativeArray because LockBufferForWrite uses write-combined
            // memory — reads are unreliable and slow. Dual-write is the correct pattern.
            NativeArray<MeshVertex> gpuVerts = _vertexBuffer.LockBufferForWrite<MeshVertex>(vOff, vertCount);

            for (int i = 0; i < vertCount; i++)
            {
                MeshVertex v = vertices[i];
                v.Position = v.Position + worldOffset;
                gpuVerts[i] = v;
                _vertexMirror[vOff + i] = v;
            }

            _vertexBuffer.UnlockBufferAfterWrite<MeshVertex>(vertCount);

            // Write offset indices to GPU and CPU mirror in one loop (was two separate loops).
            NativeArray<int> gpuIndices = _indexBuffer.LockBufferForWrite<int>(iOff, idxCount);

            for (int i = 0; i < idxCount; i++)
            {
                int idx = indices[i] + vOff;
                gpuIndices[i] = idx;
                _indexMirror[iOff + i] = idx;
            }

            _indexBuffer.UnlockBufferAfterWrite<int>(idxCount);

            PipelineStats.AddGpuUpload(vertCount * _vertexStride + idxCount * sizeof(int));
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
                    _argsDirty = true;
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

            // Dispose old GPU buffers and create new ones with larger capacity
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();

            _vertexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                newVertCap,
                _vertexStride);

            _indexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Index | GraphicsBuffer.Target.Raw,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                newIdxCap,
                sizeof(int));

            // Re-upload current data to the new GPU buffers via bulk memcpy.
            // The vertex mirror stores world-offset vertices (applied during WriteData),
            // so no per-element transform is needed here — straight copy is correct.
            if (_usedVertices > 0)
            {
                NativeArray<MeshVertex> gpuVerts = _vertexBuffer.LockBufferForWrite<MeshVertex>(0, _usedVertices);

                unsafe
                {
                    void* dstPtr = NativeArrayUnsafeUtility.GetUnsafePtr(gpuVerts);

                    fixed (MeshVertex* srcPtr = &_vertexMirror[0])
                    {
                        UnsafeUtility.MemCpy(dstPtr, srcPtr, (long)_usedVertices * _vertexStride);
                    }
                }

                _vertexBuffer.UnlockBufferAfterWrite<MeshVertex>(_usedVertices);
            }

            if (_usedIndices > 0)
            {
                NativeArray<int> gpuIdx = _indexBuffer.LockBufferForWrite<int>(0, _usedIndices);

                unsafe
                {
                    void* dstPtr = NativeArrayUnsafeUtility.GetUnsafePtr(gpuIdx);

                    fixed (int* srcPtr = &_indexMirror[0])
                    {
                        UnsafeUtility.MemCpy(dstPtr, srcPtr, (long)_usedIndices * sizeof(int));
                    }
                }

                _indexBuffer.UnlockBufferAfterWrite<int>(_usedIndices);
            }

            _vertexCapacity = newVertCap;
            _indexCapacity = newIdxCap;
            _argsDirty = true;
            PipelineStats.IncrGrow();

#if LITHFORGE_DEBUG
            UnityEngine.Debug.Log(
                $"[MegaMeshBuffer] {_name}: grew to " +
                $"{newVertCap} vertices, {newIdxCap} indices capacity");
#endif
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
