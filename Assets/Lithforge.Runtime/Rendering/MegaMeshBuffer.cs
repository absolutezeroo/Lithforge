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
        private static readonly int s_vertexStride = Marshal.SizeOf<PackedMeshVertex>();

        private readonly string _name;
        private readonly Dictionary<int3, SlotInfo> _slots = new Dictionary<int3, SlotInfo>();

        /// <summary>Free regions sorted by VertexOffset for coalescing and first-fit search.</summary>
        private readonly List<FreeRegion> _freeRegions = new List<FreeRegion>();

        private PackedMeshVertex[] _vertexMirror;
        private int[] _indexMirror;
        private int _vertexCapacity;
        private int _indexCapacity;
        private int _usedVertices;
        private int _usedIndices;
        private bool _argsDirty;

        private int _dirtyVertexMin = int.MaxValue;
        private int _dirtyVertexMax = -1;
        private int _dirtyIndexMin = int.MaxValue;
        private int _dirtyIndexMax = -1;

        private GraphicsBuffer _vertexBuffer;
        private GraphicsBuffer _indexBuffer;
        private GraphicsBuffer _argsBuffer;

        /// <summary>
        /// Cached single-element array for indirect args upload, avoiding per-frame allocation.
        /// </summary>
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _argsUploadBuffer =
            new GraphicsBuffer.IndirectDrawIndexedArgs[1];

        // --- Per-chunk indirect draw support ---
        private GraphicsBuffer _perChunkArgsBuffer;
        private int _maxChunkSlots;

        /// <summary>
        /// Cached single-element array for per-slot args upload, avoiding per-call allocation.
        /// </summary>
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _slotArgsUpload =
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

        public int IndexCapacity
        {
            get { return _indexCapacity; }
        }

        public int FreeRegionCount
        {
            get { return _freeRegions.Count; }
        }

        /// <summary>Per-chunk indirect args buffer for GPU-driven drawing.</summary>
        public GraphicsBuffer PerChunkArgsBuffer
        {
            get { return _perChunkArgsBuffer; }
        }

        /// <summary>Maximum number of per-chunk draw slots.</summary>
        public int MaxChunkSlots
        {
            get { return _maxChunkSlots; }
        }

        /// <summary>
        /// Creates a MegaMeshBuffer with the given initial capacities.
        /// The buffer grows automatically by doubling when capacity is exceeded.
        /// </summary>
        public MegaMeshBuffer(string bufferName, int initialVertexCapacity, int initialIndexCapacity,
            int maxChunkSlots)
        {
            _name = bufferName;
            _maxChunkSlots = maxChunkSlots;
            _vertexCapacity = initialVertexCapacity;
            _indexCapacity = initialIndexCapacity;
            _vertexMirror = new PackedMeshVertex[initialVertexCapacity];
            _indexMirror = new int[initialIndexCapacity];

            _vertexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                initialVertexCapacity,
                s_vertexStride);

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

            // Per-chunk indirect args buffer: one IndirectDrawIndexedArgs per slot.
            // No LockBufferForWrite — the compute shader needs RW access via RWByteAddressBuffer.
            _perChunkArgsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                maxChunkSlots,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);

            // Zero-initialize all per-chunk args slots
            GraphicsBuffer.IndirectDrawIndexedArgs[] zeroArgs =
                new GraphicsBuffer.IndirectDrawIndexedArgs[maxChunkSlots];
            _perChunkArgsBuffer.SetData(zeroArgs);

            _argsDirty = false;
        }

        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
            _argsBuffer?.Dispose();
            _perChunkArgsBuffer?.Dispose();

            _vertexBuffer = null;
            _indexBuffer = null;
            _argsBuffer = null;
            _perChunkArgsBuffer = null;

            _slots.Clear();
            _freeRegions.Clear();
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
            NativeList<PackedMeshVertex> vertices, NativeList<int> indices)
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

                        // Extend dirty range to cover the zeroed tail
                        int tailEnd = tailOffset + tailCount;
                        if (tailOffset < _dirtyIndexMin) { _dirtyIndexMin = tailOffset; }
                        if (tailEnd > _dirtyIndexMax) { _dirtyIndexMax = tailEnd; }
                    }

                    WriteDataToMirror(slot.VertexOffset, slot.IndexOffset, vertices, indices);

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

                WriteDataToMirror(region.VertexOffset, region.IndexOffset, vertices, indices);

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

            WriteDataToMirror(vOff, iOff, vertices, indices);

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
        /// Updates the indirect args buffer if dirty. Pending index zeros are now handled
        /// by FlushDirtyToGpu() via the dirty range system. Call once per frame after
        /// FlushDirtyToGpu() and before issuing draw calls.
        /// </summary>
        public void FlushArgs()
        {
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
        /// Writes vertex + index data to the CPU mirror only (no GPU upload).
        /// Expands the dirty ranges so FlushDirtyToGpu() uploads them in a single
        /// Lock/Unlock pair per buffer at the end of the frame.
        /// </summary>
        private void WriteDataToMirror(
            int vOff, int iOff,
            NativeList<PackedMeshVertex> vertices, NativeList<int> indices)
        {
            int vertCount = vertices.Length;
            int idxCount = indices.Length;

            // Vertices: bulk memcpy to CPU mirror (world offset encoded in packed vertex)
            unsafe
            {
                void* src = vertices.GetUnsafeReadOnlyPtr();
                long byteCount = (long)vertCount * s_vertexStride;

                fixed (PackedMeshVertex* mirrorPtr = &_vertexMirror[vOff])
                {
                    UnsafeUtility.MemCpy(mirrorPtr, src, byteCount);
                }
            }

            // Indices: apply global vertex offset, write to CPU mirror only
            for (int i = 0; i < idxCount; i++)
            {
                _indexMirror[iOff + i] = indices[i] + vOff;
            }

            // Expand dirty ranges
            int vEnd = vOff + vertCount;
            int iEnd = iOff + idxCount;

            if (vOff < _dirtyVertexMin) { _dirtyVertexMin = vOff; }
            if (vEnd > _dirtyVertexMax) { _dirtyVertexMax = vEnd; }
            if (iOff < _dirtyIndexMin) { _dirtyIndexMin = iOff; }
            if (iEnd > _dirtyIndexMax) { _dirtyIndexMax = iEnd; }

            PipelineStats.AddGpuUpload(vertCount * s_vertexStride + idxCount * sizeof(int));
        }

        /// <summary>
        /// Uploads all dirty vertex/index ranges to the GPU in a single Lock/Unlock pair
        /// per buffer. Call once per frame, after all AllocateOrUpdate calls are done,
        /// but before FlushArgs(). Reduces 6 Lock/Unlock per chunk to 2 total per layer.
        /// </summary>
        public void FlushDirtyToGpu()
        {
            if (_dirtyVertexMin < _dirtyVertexMax)
            {
                int count = _dirtyVertexMax - _dirtyVertexMin;
                NativeArray<PackedMeshVertex> gpu = _vertexBuffer.LockBufferForWrite<PackedMeshVertex>(
                    _dirtyVertexMin, count);

                unsafe
                {
                    void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(gpu);

                    fixed (PackedMeshVertex* src = &_vertexMirror[_dirtyVertexMin])
                    {
                        UnsafeUtility.MemCpy(dst, src, (long)count * s_vertexStride);
                    }
                }

                _vertexBuffer.UnlockBufferAfterWrite<PackedMeshVertex>(count);

                _dirtyVertexMin = int.MaxValue;
                _dirtyVertexMax = -1;
            }

            if (_dirtyIndexMin < _dirtyIndexMax)
            {
                int count = _dirtyIndexMax - _dirtyIndexMin;
                NativeArray<int> gpu = _indexBuffer.LockBufferForWrite<int>(
                    _dirtyIndexMin, count);

                unsafe
                {
                    void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(gpu);

                    fixed (int* src = &_indexMirror[_dirtyIndexMin])
                    {
                        UnsafeUtility.MemCpy(dst, src, (long)count * sizeof(int));
                    }
                }

                _indexBuffer.UnlockBufferAfterWrite<int>(count);

                _dirtyIndexMin = int.MaxValue;
                _dirtyIndexMax = -1;
            }
        }

        /// <summary>
        /// Removes a slot from the active dictionary, zeroes its indices in the CPU mirror,
        /// and returns the region to the free-list with coalescing and tail reclaim.
        /// </summary>
        private void FreeSlot(int3 coord, SlotInfo slot)
        {
            // Zero indices in CPU mirror, extend dirty range for GPU upload
            Array.Clear(_indexMirror, slot.IndexOffset, slot.IndexCount);
            int zeroEnd = slot.IndexOffset + slot.IndexCount;
            if (slot.IndexOffset < _dirtyIndexMin) { _dirtyIndexMin = slot.IndexOffset; }
            if (zeroEnd > _dirtyIndexMax) { _dirtyIndexMax = zeroEnd; }
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
                s_vertexStride);

            _indexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Index | GraphicsBuffer.Target.Raw,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                newIdxCap,
                sizeof(int));

            // Re-upload current data to the new GPU buffers via bulk memcpy.
            // The vertex mirror stores world-offset vertices (applied during WriteDataToMirror),
            // so no per-element transform is needed here — straight copy is correct.
            if (_usedVertices > 0)
            {
                NativeArray<PackedMeshVertex> gpuVerts = _vertexBuffer.LockBufferForWrite<PackedMeshVertex>(0, _usedVertices);

                unsafe
                {
                    void* dstPtr = NativeArrayUnsafeUtility.GetUnsafePtr(gpuVerts);

                    fixed (PackedMeshVertex* srcPtr = &_vertexMirror[0])
                    {
                        UnsafeUtility.MemCpy(dstPtr, srcPtr, (long)_usedVertices * s_vertexStride);
                    }
                }

                _vertexBuffer.UnlockBufferAfterWrite<PackedMeshVertex>(_usedVertices);
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

            // Grow re-uploaded everything — reset dirty ranges
            _dirtyVertexMin = int.MaxValue;
            _dirtyVertexMax = -1;
            _dirtyIndexMin = int.MaxValue;
            _dirtyIndexMax = -1;

            PipelineStats.IncrGrow();

#if LITHFORGE_DEBUG
            UnityEngine.Debug.Log(
                $"[MegaMeshBuffer] {_name}: grew to " +
                $"{newVertCap} vertices, {newIdxCap} indices capacity");
#endif
        }

        /// <summary>
        /// Updates the per-chunk indirect args entry at the given slot index.
        /// Called by ChunkMeshStore after AllocateOrUpdate to sync the per-chunk draw.
        /// The slot ID is managed externally by ChunkMeshStore (shared across all 3 layers).
        /// </summary>
        public void UpdatePerChunkArgs(int slotId, int3 coord)
        {
            if (slotId < 0 || slotId >= _maxChunkSlots)
            {
                return;
            }

            if (_slots.TryGetValue(coord, out SlotInfo slot))
            {
                _slotArgsUpload[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = (uint)slot.IndexCount,
                    instanceCount = 1,
                    startIndex = (uint)slot.IndexOffset,
                    baseVertexIndex = 0,
                    startInstance = 0,
                };
            }
            else
            {
                // No slot for this coord in this layer — zero the draw
                _slotArgsUpload[0] = default;
            }

            _perChunkArgsBuffer.SetData(_slotArgsUpload, 0, slotId, 1);
        }

        /// <summary>
        /// Grows the per-chunk args buffer to accommodate more chunk slots.
        /// Copies existing args data to the new buffer. Called by ChunkMeshStore
        /// when the slot pool is exhausted due to render distance increase.
        /// </summary>
        public void GrowSlots(int newMaxSlots)
        {
            if (newMaxSlots <= _maxChunkSlots)
            {
                return;
            }

            int oldMax = _maxChunkSlots;

            GraphicsBuffer newArgsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                newMaxSlots,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);

            // Copy old args data and zero-initialize new slots
            GraphicsBuffer.IndirectDrawIndexedArgs[] argsCopy =
                new GraphicsBuffer.IndirectDrawIndexedArgs[newMaxSlots];
            _perChunkArgsBuffer.GetData(argsCopy, 0, 0, oldMax);
            newArgsBuffer.SetData(argsCopy);

            _perChunkArgsBuffer.Dispose();
            _perChunkArgsBuffer = newArgsBuffer;
            _maxChunkSlots = newMaxSlots;
        }

        /// <summary>
        /// Zeroes the per-chunk indirect args entry at the given slot index.
        /// Called when a chunk is being destroyed.
        /// </summary>
        public void ZeroPerChunkArgs(int slotId)
        {
            if (slotId < 0 || slotId >= _maxChunkSlots)
            {
                return;
            }

            _slotArgsUpload[0] = default;
            _perChunkArgsBuffer.SetData(_slotArgsUpload, 0, slotId, 1);
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

    }
}
