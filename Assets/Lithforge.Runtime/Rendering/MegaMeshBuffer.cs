using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Lithforge.Meshing;
using Lithforge.Runtime.Debug;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

using UnityEngine;
using UnityEngine.Profiling;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    ///     Owns persistent GPU buffers (vertex + index + indirect args) for one render layer
    ///     (opaque, cutout, or translucent). Freed slots are recycled via a coalescing
    ///     free-list so the buffer stays bounded during player movement. When a chunk is
    ///     re-meshed and the new data fits in the existing slot, it is rewritten in-place
    ///     with no free-list interaction. Indices of freed regions are zeroed (batched per
    ///     frame) to produce degenerate triangles until the slot is reclaimed.
    ///     CPU-side NativeArray mirrors avoid GPU readback. Dirty sub-ranges are tracked
    ///     as disjoint intervals and uploaded via SetData(NativeArray) to VRAM-resident buffers.
    ///     Owner: ChunkMeshStore. Lifetime: application session.
    /// </summary>
    public sealed class MegaMeshBuffer : IDisposable
    {
        private static readonly int s_vertexStride = Marshal.SizeOf<PackedMeshVertex>();

        /// <summary>
        ///     Cached single-element array for indirect args upload, avoiding per-frame allocation.
        /// </summary>
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _argsUploadBuffer =
            new GraphicsBuffer.IndirectDrawIndexedArgs[1];

        /// <summary>Free regions sorted by VertexOffset for coalescing and first-fit search.</summary>
        private readonly List<FreeRegion> _freeRegions = new();

        /// <summary>Debug/log label identifying this layer (e.g. "Opaque", "Cutout").</summary>
        private readonly string _name;

        private readonly IPipelineStats _pipelineStats;

        /// <summary>GPU buffer resize service — dispatches compute copy and defers disposal.</summary>
        private readonly GpuBufferResizer _resizer;

        /// <summary>
        ///     Cached single-element array for per-slot args upload, avoiding per-call allocation.
        /// </summary>
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _slotArgsUpload =
            new GraphicsBuffer.IndirectDrawIndexedArgs[1];

        /// <summary>Maps chunk coord to its vertex/index region in the shared buffers.</summary>
        private readonly Dictionary<int3, SlotInfo> _slots = new();

        /// <summary>Single-element indirect args buffer for the whole-layer draw call.</summary>
        private GraphicsBuffer _argsBuffer;

        /// <summary>Set when any slot changes; cleared after FlushArgs uploads the indirect args.</summary>
        private bool _argsDirty;

        /// <summary>Disjoint dirty intervals in the index mirror awaiting GPU upload.</summary>
        private DirtyRangeList _dirtyIndexRanges;

        /// <summary>Disjoint dirty intervals in the vertex mirror awaiting GPU upload.</summary>
        private DirtyRangeList _dirtyVertexRanges;

        /// <summary>VRAM-resident index buffer (also Raw-flagged for compute access).</summary>
        private GraphicsBuffer _indexBuffer;

        /// <summary>CPU-side mirror of GPU index data, with global vertex offsets already applied.</summary>
        private NativeArray<int> _indexMirror;

        /// <summary>Current capacity of the per-chunk args buffer, grown by GrowSlots.</summary>
        private int _maxChunkSlots;

        // --- Per-chunk indirect draw support ---

        /// <summary>One IndirectDrawIndexedArgs per chunk slot, written by the compute culling shader.</summary>
        private GraphicsBuffer _perChunkArgsBuffer;

        /// <summary>High-water mark in index space -- next append goes here.</summary>
        private int _usedIndices;

        /// <summary>High-water mark in vertex space -- next append goes here.</summary>
        private int _usedVertices;

        /// <summary>VRAM-resident structured buffer holding PackedMeshVertex data.</summary>
        private GraphicsBuffer _vertexBuffer;

        /// <summary>CPU-side mirror of GPU vertex data, enabling sub-range uploads without GPU readback.</summary>
        private NativeArray<PackedMeshVertex> _vertexMirror;

        /// <summary>
        ///     Creates a MegaMeshBuffer with the given initial capacities.
        ///     The buffer grows automatically by doubling when capacity is exceeded.
        /// </summary>
        public MegaMeshBuffer(string bufferName, int initialVertexCapacity, int initialIndexCapacity,
            int maxChunkSlots, GpuBufferResizer resizer, IPipelineStats pipelineStats)
        {
            _name = bufferName;
            _pipelineStats = pipelineStats;
            _resizer = resizer;
            _maxChunkSlots = maxChunkSlots;
            VertexCapacity = initialVertexCapacity;
            IndexCapacity = initialIndexCapacity;
            _vertexMirror = new NativeArray<PackedMeshVertex>(initialVertexCapacity, Allocator.Persistent);
            _indexMirror = new NativeArray<int>(initialIndexCapacity, Allocator.Persistent);

            _vertexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.Raw,
                GraphicsBuffer.UsageFlags.None,
                initialVertexCapacity,
                s_vertexStride);

            _indexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Index | GraphicsBuffer.Target.Raw,
                GraphicsBuffer.UsageFlags.None,
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

        /// <summary>GPU structured buffer bound as the vertex source for indirect draws.</summary>
        public GraphicsBuffer VertexBuffer
        {
            get { return _vertexBuffer; }
        }

        /// <summary>GPU index buffer bound for RenderPrimitivesIndexedIndirect calls.</summary>
        public GraphicsBuffer IndexBuffer
        {
            get { return _indexBuffer; }
        }

        /// <summary>Whole-layer indirect args buffer (single draw covering all slots).</summary>
        public GraphicsBuffer ArgsBuffer
        {
            get { return _argsBuffer; }
        }

        /// <summary>True when at least one chunk slot is allocated, meaning there is something to draw.</summary>
        public bool HasGeometry
        {
            get { return _slots.Count > 0; }
        }

        /// <summary>Number of vertex entries currently occupied (active slots + freed-but-not-reclaimed).</summary>
        public int UsedVertices
        {
            get { return _usedVertices; }
        }

        /// <summary>Total vertex capacity of the backing buffers before a grow is required.</summary>
        public int VertexCapacity { get; private set; }

        /// <summary>Total index capacity of the backing buffers before a grow is required.</summary>
        public int IndexCapacity { get; private set; }

        /// <summary>Number of disjoint free regions in the coalescing free-list, useful for fragmentation diagnostics.</summary>
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
        ///     Releases all GPU buffers and CPU-side NativeArrays. Safe to call multiple times.
        /// </summary>
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

            if (_vertexMirror.IsCreated)
            {
                _vertexMirror.Dispose();
            }

            if (_indexMirror.IsCreated)
            {
                _indexMirror.Dispose();
            }
        }

        /// <summary>
        ///     Allocates a new slot or updates an existing one for the given chunk coordinate.
        ///     Three allocation paths in priority order:
        ///     1. In-place reuse — if the chunk already has a slot and the new data fits, overwrite it.
        ///     2. Free-list first-fit — search the coalescing free-list for a region that fits.
        ///     3. Append — add at the end, growing the buffer if necessary.
        ///     Empty meshes (zero vertices) free any existing slot and return immediately.
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

                        unsafe
                        {
                            int* ptr = (int*)_indexMirror.GetUnsafePtr() + tailOffset;
                            UnsafeUtility.MemClear(ptr, (long)tailCount * sizeof(int));
                        }

                        int tailEnd = tailOffset + tailCount;
                        _dirtyIndexRanges.Add(tailOffset, tailEnd);
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
                    FreeRegion remainder = new()
                    {
                        VertexOffset = region.VertexOffset + vertCount, VertexCapacity = leftoverVerts, IndexOffset = region.IndexOffset + idxCount, IndexCapacity = leftoverIdx,
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
        ///     Frees the slot for the given chunk coordinate, returning it to the free-list.
        ///     No-op if the chunk has no slot.
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
        ///     Updates the indirect args buffer if dirty. Pending index zeros are now handled
        ///     by FlushDirtyToGpu() via the dirty range system. Call once per frame after
        ///     FlushDirtyToGpu() and before issuing draw calls.
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
        ///     Writes vertex + index data to the CPU mirror only (no GPU upload).
        ///     Adds dirty sub-ranges so FlushDirtyToGpu() uploads only the modified
        ///     regions via SetData(NativeArray) at the end of the frame.
        /// </summary>
        private void WriteDataToMirror(
            int vOff, int iOff,
            NativeList<PackedMeshVertex> vertices, NativeList<int> indices)
        {
            int vertCount = vertices.Length;
            int idxCount = indices.Length;

            unsafe
            {
                // Vertices: bulk memcpy to CPU mirror (world offset encoded in packed vertex)
                void* vertSrc = vertices.GetUnsafeReadOnlyPtr();
                PackedMeshVertex* vertDst =
                    (PackedMeshVertex*)_vertexMirror.GetUnsafePtr() + vOff;
                UnsafeUtility.MemCpy(vertDst, vertSrc, (long)vertCount * s_vertexStride);

                // Indices: apply global vertex offset, write to CPU mirror
                int* idxDst = (int*)_indexMirror.GetUnsafePtr() + iOff;
                int* idxSrc = indices.GetUnsafeReadOnlyPtr();

                for (int i = 0; i < idxCount; i++)
                {
                    idxDst[i] = idxSrc[i] + vOff;
                }
            }

            // Track dirty sub-ranges for per-range SetData upload
            _dirtyVertexRanges.Add(vOff, vOff + vertCount);
            _dirtyIndexRanges.Add(iOff, iOff + idxCount);

            _pipelineStats.AddGpuUpload(vertCount * s_vertexStride + idxCount * sizeof(int));
        }

        /// <summary>
        ///     Uploads dirty vertex/index sub-ranges to the GPU via SetData(NativeArray).
        ///     Each disjoint dirty interval produces one SetData call, avoiding upload of
        ///     untouched gaps between distant dirty chunks. Call once per frame after all
        ///     AllocateOrUpdate calls are done, but before FlushArgs().
        /// </summary>
        public void FlushDirtyToGpu()
        {
            Profiler.BeginSample("MMB.Upload");

            for (int i = 0; i < _dirtyVertexRanges.Count; i++)
            {
                DirtyRange range = _dirtyVertexRanges[i];
                int count = range.End - range.Start;
                _vertexBuffer.SetData(_vertexMirror, range.Start, range.Start, count);
            }

            _dirtyVertexRanges.Clear();

            for (int i = 0; i < _dirtyIndexRanges.Count; i++)
            {
                DirtyRange range = _dirtyIndexRanges[i];
                int count = range.End - range.Start;
                _indexBuffer.SetData(_indexMirror, range.Start, range.Start, count);
            }

            _dirtyIndexRanges.Clear();

            Profiler.EndSample();
        }

        /// <summary>
        ///     Removes a slot from the active dictionary, zeroes its indices in the CPU mirror,
        ///     and returns the region to the free-list with coalescing and tail reclaim.
        /// </summary>
        private void FreeSlot(int3 coord, SlotInfo slot)
        {
            Profiler.BeginSample("MMB.FreeSlot");

            // Zero indices in CPU mirror, track dirty range for GPU upload
            unsafe
            {
                int* ptr = (int*)_indexMirror.GetUnsafePtr() + slot.IndexOffset;
                UnsafeUtility.MemClear(ptr, (long)slot.IndexCount * sizeof(int));
            }

            int zeroEnd = slot.IndexOffset + slot.IndexCount;
            _dirtyIndexRanges.Add(slot.IndexOffset, zeroEnd);
            _slots.Remove(coord);

            // Insert into free-list sorted by VertexOffset
            FreeRegion region = new()
            {
                VertexOffset = slot.VertexOffset, VertexCapacity = slot.VertexCapacity, IndexOffset = slot.IndexOffset, IndexCapacity = slot.IndexCapacity,
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

            Profiler.EndSample();
        }

        /// <summary>
        ///     Merges the free region at the given index with its immediate neighbors
        ///     if they are contiguous in both vertex and index space.
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
                        VertexOffset = current.VertexOffset, VertexCapacity = current.VertexCapacity + next.VertexCapacity, IndexOffset = current.IndexOffset, IndexCapacity = current.IndexCapacity + next.IndexCapacity,
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
                        VertexOffset = prev.VertexOffset, VertexCapacity = prev.VertexCapacity + current.VertexCapacity, IndexOffset = prev.IndexOffset, IndexCapacity = prev.IndexCapacity + current.IndexCapacity,
                    };
                    _freeRegions.RemoveAt(idx);
                }
            }
        }

        /// <summary>
        ///     Searches the free-list for the first region that can hold the requested
        ///     vertex and index counts. Returns the index into _freeRegions, or -1.
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
        ///     Ensures the buffer has room for the given number of additional vertices and indices
        ///     at the append position. Grows by doubling if capacity is exceeded.
        /// </summary>
        private void EnsureCapacity(int extraVertices, int extraIndices)
        {
            bool vertsFit = _usedVertices + extraVertices <= VertexCapacity;
            bool idxFit = _usedIndices + extraIndices <= IndexCapacity;

            if (vertsFit && idxFit)
            {
                return;
            }

            Grow(extraVertices, extraIndices);
        }

        /// <summary>
        ///     Grows the buffer capacity by doubling (or more if the required extra exceeds a double).
        ///     GPU buffers are resized via compute-shader copy (no CPU re-upload stall). Old GPU
        ///     buffers are retired for deferred disposal to avoid use-after-free on the GPU.
        ///     Dirty ranges are NOT cleared — they may contain data written to the CPU mirror
        ///     this frame that has not yet been flushed to the old GPU buffer, so the compute
        ///     copy did not transfer it. FlushDirtyToGpu must still upload those ranges.
        /// </summary>
        private void Grow(int extraVertices, int extraIndices)
        {
            int newVertCap = math.max(VertexCapacity * 2, _usedVertices + extraVertices);
            int newIdxCap = math.max(IndexCapacity * 2, _usedIndices + extraIndices);

            // Resize vertex mirror: allocate new, copy used portion, dispose old
            NativeArray<PackedMeshVertex> newVertexMirror =
                new(newVertCap, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);

            if (_usedVertices > 0)
            {
                unsafe
                {
                    UnsafeUtility.MemCpy(
                        newVertexMirror.GetUnsafePtr(),
                        _vertexMirror.GetUnsafeReadOnlyPtr(),
                        (long)_usedVertices * s_vertexStride);
                }
            }

            _vertexMirror.Dispose();
            _vertexMirror = newVertexMirror;

            // Resize index mirror: allocate new, copy used portion, dispose old
            NativeArray<int> newIndexMirror =
                new(newIdxCap, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);

            if (_usedIndices > 0)
            {
                unsafe
                {
                    UnsafeUtility.MemCpy(
                        newIndexMirror.GetUnsafePtr(),
                        _indexMirror.GetUnsafeReadOnlyPtr(),
                        (long)_usedIndices * sizeof(int));
                }
            }

            _indexMirror.Dispose();
            _indexMirror = newIndexMirror;

            // GPU buffer resize: compute copy old→new, deferred disposal of old buffers.
            // No SetData re-upload — the compute shader copies existing data on the GPU.
            // Old buffers survive for 3 frames so the GPU finishes any in-flight reads.
            _vertexBuffer = _resizer.Resize(
                _vertexBuffer,
                newVertCap,
                _usedVertices,
                GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.Raw,
                s_vertexStride);

            _indexBuffer = _resizer.Resize(
                _indexBuffer,
                newIdxCap,
                _usedIndices,
                GraphicsBuffer.Target.Index | GraphicsBuffer.Target.Raw,
                sizeof(int));

            VertexCapacity = newVertCap;
            IndexCapacity = newIdxCap;
            _argsDirty = true;

            _pipelineStats.IncrGrow();

#if LITHFORGE_DEBUG
            UnityEngine.Debug.Log(
                $"[MegaMeshBuffer] {_name}: grew to " +
                $"{newVertCap} vertices, {newIdxCap} indices capacity");
#endif
        }

        /// <summary>
        ///     Updates the per-chunk indirect args entry at the given slot index.
        ///     Called by ChunkMeshStore after AllocateOrUpdate to sync the per-chunk draw.
        ///     The slot ID is managed externally by ChunkMeshStore (shared across all 3 layers).
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
        ///     Grows the per-chunk args buffer to accommodate more chunk slots.
        ///     Existing args data is copied via GPU compute dispatch (no blocking readback).
        ///     Old buffer is retired for deferred disposal. Called by ChunkMeshStore
        ///     when the slot pool is exhausted due to render distance increase.
        /// </summary>
        public void GrowSlots(int newMaxSlots)
        {
            if (newMaxSlots <= _maxChunkSlots)
            {
                return;
            }

            _perChunkArgsBuffer = _resizer.Resize(
                _perChunkArgsBuffer,
                newMaxSlots,
                _maxChunkSlots,
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);

            _maxChunkSlots = newMaxSlots;
        }

        /// <summary>
        ///     Zeroes the per-chunk indirect args entry at the given slot index.
        ///     Called when a chunk is being destroyed.
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

        /// <summary>
        ///     Tracks the vertex/index region owned by a single chunk in the shared buffers.
        ///     Capacity may exceed Count when in-place reuse keeps the larger original allocation.
        /// </summary>
        private struct SlotInfo
        {
            public int VertexOffset;
            public int VertexCount;
            public int VertexCapacity;
            public int IndexOffset;
            public int IndexCount;
            public int IndexCapacity;
        }

        /// <summary>
        ///     A recycled region in the free-list, sorted by VertexOffset for coalescing.
        ///     Paired vertex/index ranges stay together to simplify first-fit allocation.
        /// </summary>
        private struct FreeRegion
        {
            public int VertexOffset;
            public int VertexCapacity;
            public int IndexOffset;
            public int IndexCapacity;
        }

        /// <summary>Half-open interval [Start, End) marking a dirty region in the CPU mirror.</summary>
        private struct DirtyRange
        {
            public int Start;
            public int End;
        }

        /// <summary>
        ///     Tracks up to 16 disjoint dirty [Start, End) intervals, sorted by Start.
        ///     Overlapping or adjacent ranges are merged on insertion. If the number of
        ///     ranges would exceed the capacity, all ranges collapse into a single
        ///     bounding interval (graceful degradation). Typical frame produces 1–10 ranges.
        /// </summary>
        private struct DirtyRangeList
        {
            private const int MaxRanges = 16;

            private DirtyRange[] _ranges;

            /// <summary>Number of disjoint dirty intervals currently tracked.</summary>
            public int Count { get; private set; }

            /// <summary>Returns the dirty interval at the given position in the sorted list.</summary>
            public DirtyRange this[int index]
            {
                get
                {
                    return _ranges[index];
                }
            }

            /// <summary>
            ///     Adds a dirty interval [start, end). Merges with any overlapping or
            ///     adjacent existing ranges. Collapses to one bounding range on overflow.
            /// </summary>
            public void Add(int start, int end)
            {
                if (start >= end)
                {
                    return;
                }

                if (_ranges == null)
                {
                    _ranges = new DirtyRange[MaxRanges];
                }

                int mergedStart = start;
                int mergedEnd = end;
                int writeIdx = 0;

                // Compact: keep non-overlapping ranges, merge overlapping ones
                for (int i = 0; i < Count; i++)
                {
                    DirtyRange r = _ranges[i];

                    if (r.Start <= mergedEnd && r.End >= mergedStart)
                    {
                        mergedStart = math.min(mergedStart, r.Start);
                        mergedEnd = math.max(mergedEnd, r.End);
                    }
                    else
                    {
                        _ranges[writeIdx] = r;
                        writeIdx++;
                    }
                }

                if (writeIdx < MaxRanges)
                {
                    // Insert merged range in sorted position
                    int insertAt = writeIdx;

                    for (int i = 0; i < writeIdx; i++)
                    {
                        if (_ranges[i].Start > mergedStart)
                        {
                            insertAt = i;
                            break;
                        }
                    }

                    for (int i = writeIdx; i > insertAt; i--)
                    {
                        _ranges[i] = _ranges[i - 1];
                    }

                    _ranges[insertAt] = new DirtyRange
                    {
                        Start = mergedStart, End = mergedEnd,
                    };
                    Count = writeIdx + 1;
                }
                else
                {
                    // Overflow: collapse everything into one bounding range
                    for (int i = 0; i < writeIdx; i++)
                    {
                        mergedStart = math.min(mergedStart, _ranges[i].Start);
                        mergedEnd = math.max(mergedEnd, _ranges[i].End);
                    }

                    _ranges[0] = new DirtyRange
                    {
                        Start = mergedStart, End = mergedEnd,
                    };
                    Count = 1;
                }
            }

            /// <summary>Resets the list to empty without releasing the backing array.</summary>
            public void Clear()
            {
                Count = 0;
            }
        }
    }
}
