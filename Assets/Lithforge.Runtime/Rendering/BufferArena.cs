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
    ///     One GPU vertex+index buffer pair managed by decoupled TLSF allocators.
    ///     Vertex and index address spaces are independent — they are allocated from
    ///     separate TlsfAllocator instances. CPU-side NativeArray mirrors avoid GPU readback.
    ///     Dirty sub-ranges are tracked as disjoint intervals and uploaded via
    ///     SetData(NativeArray) to VRAM-resident buffers.
    ///     Does NOT own a per-chunk args buffer; that is owned by BufferArenaPool.
    ///     Owner: BufferArenaPool. Lifetime: application session.
    /// </summary>
    internal sealed class BufferArena : IDisposable
    {
        /// <summary>Byte stride of a single PackedMeshVertex, cached for buffer sizing and upload math.</summary>
        private static readonly int s_vertexStride = Marshal.SizeOf<PackedMeshVertex>();

        /// <summary>TLSF allocator over the vertex address space [0, VertexCapacity).</summary>
        private TlsfAllocator _vertexAllocator;

        /// <summary>TLSF allocator over the index address space [0, IndexCapacity).</summary>
        private TlsfAllocator _indexAllocator;

        /// <summary>Maps chunk coord to its vertex/index region in this arena.</summary>
        private readonly Dictionary<int3, ArenaSlot> _slots = new();

        /// <summary>Debug/log label identifying this arena (e.g. "Opaque_Arena0").</summary>
        private readonly string _name;

        /// <summary>Pipeline stats sink for recording GPU upload bytes.</summary>
        private readonly IPipelineStats _pipelineStats;

        /// <summary>GPU buffer resize service — dispatches compute copy and defers disposal.</summary>
        private readonly GpuBufferResizer _resizer;

        /// <summary>Maximum vertex capacity this arena can grow to (platform-derived cap).</summary>
        private readonly int _maxVertexCapacity;

        /// <summary>Maximum index capacity this arena can grow to (platform-derived cap).</summary>
        private readonly int _maxIndexCapacity;

        /// <summary>VRAM-resident structured buffer holding PackedMeshVertex data.</summary>
        private GraphicsBuffer _vertexBuffer;

        /// <summary>VRAM-resident index buffer (also Raw-flagged for compute access).</summary>
        private GraphicsBuffer _indexBuffer;

        /// <summary>CPU-side mirror of GPU vertex data, enabling sub-range uploads without GPU readback.</summary>
        private NativeArray<PackedMeshVertex> _vertexMirror;

        /// <summary>CPU-side mirror of GPU index data, with global vertex offsets already applied.</summary>
        private NativeArray<int> _indexMirror;

        /// <summary>Disjoint dirty intervals in the vertex mirror awaiting GPU upload.</summary>
        private DirtyRangeList _dirtyVertexRanges;

        /// <summary>Disjoint dirty intervals in the index mirror awaiting GPU upload.</summary>
        private DirtyRangeList _dirtyIndexRanges;

        /// <summary>
        ///     Creates a BufferArena with the given initial capacities and maximum growth caps.
        ///     The arena starts at initial capacity and can grow up to the max cap by doubling.
        /// </summary>
        public BufferArena(
            string arenaName,
            int initialVertexCapacity, int initialIndexCapacity,
            int maxVertexCapacity, int maxIndexCapacity,
            GpuBufferResizer resizer, IPipelineStats pipelineStats)
        {
            _name = arenaName;
            _pipelineStats = pipelineStats;
            _resizer = resizer;
            _maxVertexCapacity = maxVertexCapacity;
            _maxIndexCapacity = maxIndexCapacity;
            VertexCapacity = initialVertexCapacity;
            IndexCapacity = initialIndexCapacity;

            _vertexAllocator = new TlsfAllocator(initialVertexCapacity);
            _indexAllocator = new TlsfAllocator(initialIndexCapacity);

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

        /// <summary>True when at least one chunk slot is allocated in this arena.</summary>
        public bool HasGeometry
        {
            get { return _slots.Count > 0; }
        }

        /// <summary>Total vertex capacity of the backing GPU buffers.</summary>
        public int VertexCapacity { get; private set; }

        /// <summary>Total index capacity of the backing GPU buffers.</summary>
        public int IndexCapacity { get; private set; }

        /// <summary>Number of chunks currently occupying slots in this arena.</summary>
        public int SlotCount
        {
            get { return _slots.Count; }
        }

        /// <summary>Number of free vertex elements available in the TLSF allocator.</summary>
        public int FreeVertexElements
        {
            get { return _vertexAllocator.FreeElements; }
        }

        /// <summary>Number of free index elements available in the TLSF allocator.</summary>
        public int FreeIndexElements
        {
            get { return _indexAllocator.FreeElements; }
        }

        /// <summary>Whether this arena has reached its maximum growth capacity.</summary>
        public bool IsAtMaxCapacity
        {
            get { return VertexCapacity >= _maxVertexCapacity && IndexCapacity >= _maxIndexCapacity; }
        }

        /// <summary>
        ///     Fraction of vertex capacity currently in use (1.0 = fully occupied).
        /// </summary>
        public float VertexPressure
        {
            get
            {
                if (VertexCapacity == 0)
                {
                    return 0f;
                }

                return (float)_vertexAllocator.UsedElements / VertexCapacity;
            }
        }

        /// <summary>
        ///     Allocates a new slot or updates an existing one for the given chunk coordinate.
        ///     Returns true if the allocation succeeded, false if the arena is full and cannot
        ///     grow further (caller should try the next arena or create a new one).
        ///     Three allocation paths in priority order:
        ///     1. In-place reuse — existing slot's TLSF block is large enough.
        ///     2. Free existing slot, then TLSF alloc — old slot too small.
        ///     3. TLSF alloc in new space (may trigger Grow).
        ///     Empty meshes (zero vertices) free any existing slot and return true.
        /// </summary>
        public bool AllocateOrUpdate(
            int3 coord,
            NativeList<PackedMeshVertex> vertices, NativeList<int> indices)
        {
            int vertCount = vertices.Length;
            int idxCount = indices.Length;

            // Empty mesh — free existing slot if any
            if (vertCount == 0)
            {
                Free(coord);
                return true;
            }

            // Path 1: In-place reuse — chunk already has a slot and TLSF block is large enough
            if (_slots.TryGetValue(coord, out ArenaSlot existing))
            {
                int existingVertBlockSize = _vertexAllocator.SizeAt(existing.VertexOffset);
                int existingIdxBlockSize = _indexAllocator.SizeAt(existing.IndexOffset);

                if (vertCount <= existingVertBlockSize && idxCount <= existingIdxBlockSize)
                {
                    // Clear old index tail if the new mesh is shorter
                    if (idxCount < existing.IndexCount)
                    {
                        int tailOffset = existing.IndexOffset + idxCount;
                        int tailCount = existing.IndexCount - idxCount;

                        unsafe
                        {
                            int* ptr = (int*)_indexMirror.GetUnsafePtr() + tailOffset;
                            UnsafeUtility.MemClear(ptr, (long)tailCount * sizeof(int));
                        }

                        int tailEnd = tailOffset + tailCount;
                        _dirtyIndexRanges.Add(tailOffset, tailEnd);
                    }

                    WriteDataToMirror(existing.VertexOffset, existing.IndexOffset, vertices, indices);

                    _slots[coord] = new ArenaSlot
                    {
                        VertexOffset = existing.VertexOffset,
                        VertexCount = vertCount,
                        IndexOffset = existing.IndexOffset,
                        IndexCount = idxCount,
                    };

                    return true;
                }

                // Old slot too small — free it and fall through to new allocation
                FreeSlotInternal(coord, existing);
            }

            // Path 2/3: TLSF allocation (may trigger Grow if needed)
            return TryAllocNew(coord, vertices, indices);
        }

        /// <summary>
        ///     Frees the slot for the given chunk coordinate, returning the TLSF blocks
        ///     to the free pool. No-op if the chunk has no slot in this arena.
        /// </summary>
        public void Free(int3 coord)
        {
            if (_slots.TryGetValue(coord, out ArenaSlot slot))
            {
                FreeSlotInternal(coord, slot);
            }
        }

        /// <summary>
        ///     Returns true if this arena contains a slot for the given chunk coordinate.
        /// </summary>
        public bool ContainsChunk(int3 coord)
        {
            return _slots.ContainsKey(coord);
        }

        /// <summary>
        ///     Returns the ArenaSlot for the given coord, or false if not present.
        ///     Used by BufferArenaPool to build per-chunk draw args.
        /// </summary>
        public bool TryGetSlot(int3 coord, out ArenaSlot slot)
        {
            return _slots.TryGetValue(coord, out slot);
        }

        /// <summary>
        ///     Returns a read-only view of all chunk coordinates in this arena.
        ///     Used by BufferArenaPool for distance-based eviction scanning.
        /// </summary>
        public Dictionary<int3, ArenaSlot>.KeyCollection ChunkCoords
        {
            get { return _slots.Keys; }
        }

        /// <summary>
        ///     Uploads dirty vertex/index sub-ranges to the GPU via SetData(NativeArray).
        ///     Each disjoint dirty interval produces one SetData call. Call once per frame
        ///     after all AllocateOrUpdate calls are done.
        /// </summary>
        public void FlushDirtyToGpu()
        {
            Profiler.BeginSample("Arena.Upload");

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

        /// <summary>Releases all GPU buffers and CPU-side NativeArrays. Safe to call multiple times.</summary>
        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();

            _vertexBuffer = null;
            _indexBuffer = null;

            _slots.Clear();

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
        ///     Attempts to allocate new TLSF blocks for the given chunk data.
        ///     If the allocator reports no fit and the arena can still grow, doubles capacity
        ///     and retries. Returns false only if the arena is at max capacity and cannot fit.
        /// </summary>
        private bool TryAllocNew(
            int3 coord,
            NativeList<PackedMeshVertex> vertices, NativeList<int> indices)
        {
            int vertCount = vertices.Length;
            int idxCount = indices.Length;

            int vOff = _vertexAllocator.Alloc(vertCount);
            int iOff = vOff != -1 ? _indexAllocator.Alloc(idxCount) : -1;

            // If either allocation failed, try growing the arena
            if (vOff == -1 || iOff == -1)
            {
                // Roll back partial allocations
                if (vOff != -1)
                {
                    _vertexAllocator.Free(vOff);
                }

                if (iOff != -1)
                {
                    _indexAllocator.Free(iOff);
                }

                // Try to grow and retry
                if (!TryGrow(vertCount, idxCount))
                {
                    return false; // Cannot grow further
                }

                // Retry allocation after grow
                vOff = _vertexAllocator.Alloc(vertCount);
                iOff = vOff != -1 ? _indexAllocator.Alloc(idxCount) : -1;

                if (vOff == -1 || iOff == -1)
                {
                    // Roll back again
                    if (vOff != -1)
                    {
                        _vertexAllocator.Free(vOff);
                    }

                    if (iOff != -1)
                    {
                        _indexAllocator.Free(iOff);
                    }

                    return false; // Still doesn't fit even after grow
                }
            }

            WriteDataToMirror(vOff, iOff, vertices, indices);

            _slots[coord] = new ArenaSlot
            {
                VertexOffset = vOff,
                VertexCount = vertCount,
                IndexOffset = iOff,
                IndexCount = idxCount,
            };

            return true;
        }

        /// <summary>
        ///     Attempts to grow the arena's capacity by doubling, up to the platform cap.
        ///     Returns true if growth occurred, false if already at max capacity.
        /// </summary>
        private bool TryGrow(int minExtraVerts, int minExtraIndices)
        {
            bool canGrowVerts = VertexCapacity < _maxVertexCapacity;
            bool canGrowIdx = IndexCapacity < _maxIndexCapacity;

            if (!canGrowVerts && !canGrowIdx)
            {
                return false;
            }

            int newVertCap = VertexCapacity;
            int newIdxCap = IndexCapacity;

            if (canGrowVerts)
            {
                newVertCap = math.min(
                    math.max(VertexCapacity * 2, VertexCapacity + minExtraVerts),
                    _maxVertexCapacity);
            }

            if (canGrowIdx)
            {
                newIdxCap = math.min(
                    math.max(IndexCapacity * 2, IndexCapacity + minExtraIndices),
                    _maxIndexCapacity);
            }

            if (newVertCap == VertexCapacity && newIdxCap == IndexCapacity)
            {
                return false;
            }

            Grow(newVertCap, newIdxCap);
            return true;
        }

        /// <summary>
        ///     Grows the arena's GPU buffers and TLSF allocators to the specified capacities.
        ///     GPU buffers are resized via compute-shader copy. CPU mirrors are reallocated.
        /// </summary>
        private void Grow(int newVertCap, int newIdxCap)
        {
            int oldVertCap = VertexCapacity;
            int oldIdxCap = IndexCapacity;

            // Grow vertex side
            if (newVertCap > oldVertCap)
            {
                NativeArray<PackedMeshVertex> newVertexMirror =
                    new(newVertCap, Allocator.Persistent,
                        NativeArrayOptions.UninitializedMemory);

                if (oldVertCap > 0)
                {
                    unsafe
                    {
                        UnsafeUtility.MemCpy(
                            newVertexMirror.GetUnsafePtr(),
                            _vertexMirror.GetUnsafeReadOnlyPtr(),
                            (long)oldVertCap * s_vertexStride);
                    }
                }

                _vertexMirror.Dispose();
                _vertexMirror = newVertexMirror;

                _vertexBuffer = _resizer.Resize(
                    _vertexBuffer,
                    newVertCap,
                    oldVertCap,
                    GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.Raw,
                    s_vertexStride);

                _vertexAllocator.Grow(newVertCap);
                VertexCapacity = newVertCap;
            }

            // Grow index side
            if (newIdxCap > oldIdxCap)
            {
                NativeArray<int> newIndexMirror =
                    new(newIdxCap, Allocator.Persistent,
                        NativeArrayOptions.UninitializedMemory);

                if (oldIdxCap > 0)
                {
                    unsafe
                    {
                        UnsafeUtility.MemCpy(
                            newIndexMirror.GetUnsafePtr(),
                            _indexMirror.GetUnsafeReadOnlyPtr(),
                            (long)oldIdxCap * sizeof(int));
                    }
                }

                _indexMirror.Dispose();
                _indexMirror = newIndexMirror;

                _indexBuffer = _resizer.Resize(
                    _indexBuffer,
                    newIdxCap,
                    oldIdxCap,
                    GraphicsBuffer.Target.Index | GraphicsBuffer.Target.Raw,
                    sizeof(int));

                _indexAllocator.Grow(newIdxCap);
                IndexCapacity = newIdxCap;
            }

            _pipelineStats.IncrGrow();

#if LITHFORGE_DEBUG
            UnityEngine.Debug.Log(
                $"[BufferArena] {_name}: grew to " +
                $"{VertexCapacity} vertices, {IndexCapacity} indices capacity");
#endif
        }

        /// <summary>
        ///     Removes a slot from the active dictionary, zeroes its indices in the CPU mirror,
        ///     and returns the TLSF blocks to the free pool.
        /// </summary>
        private void FreeSlotInternal(int3 coord, ArenaSlot slot)
        {
            Profiler.BeginSample("Arena.FreeSlot");

            // Zero indices in CPU mirror, track dirty range for GPU upload
            unsafe
            {
                int* ptr = (int*)_indexMirror.GetUnsafePtr() + slot.IndexOffset;
                UnsafeUtility.MemClear(ptr, (long)slot.IndexCount * sizeof(int));
            }

            int zeroEnd = slot.IndexOffset + slot.IndexCount;
            _dirtyIndexRanges.Add(slot.IndexOffset, zeroEnd);

            _slots.Remove(coord);

            // Return blocks to TLSF allocators
            _vertexAllocator.Free(slot.VertexOffset);
            _indexAllocator.Free(slot.IndexOffset);

            Profiler.EndSample();
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
                // Vertices: bulk memcpy to CPU mirror
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

        /// <summary>Half-open interval [Start, End) marking a dirty region in the CPU mirror.</summary>
        private struct DirtyRange
        {
            /// <summary>Inclusive start index of the dirty interval.</summary>
            public int Start;

            /// <summary>Exclusive end index of the dirty interval.</summary>
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
            /// <summary>Maximum number of disjoint intervals before collapsing to a single bounding range.</summary>
            private const int MaxRanges = 16;

            /// <summary>Backing array for the sorted dirty intervals, lazily allocated on first Add.</summary>
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

                if (_ranges is null)
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
