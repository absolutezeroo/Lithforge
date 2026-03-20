using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Lithforge.Meshing;
using Lithforge.Runtime.Debug;

using Unity.Collections;
using Unity.Mathematics;

using UnityEngine;
using UnityEngine.Profiling;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    ///     Manages a dynamic list of GPU buffer arenas for one render layer
    ///     (opaque, cutout, or translucent). When a chunk cannot fit in any existing
    ///     arena, a new arena is allocated. Arena capacity is derived from
    ///     SystemInfo.maxGraphicsBufferSize / 4, capped at 512 MB.
    ///     Each arena has its own per-chunk indirect args buffer indexed by cull slot ID
    ///     (shared with ChunkMeshStore). This means all arenas' args buffers are the same
    ///     size (maxChunkSlots) and the compute culling shader is unchanged.
    ///     Owner: ChunkMeshStore. Lifetime: application session.
    /// </summary>
    public sealed class BufferArenaPool : IDisposable
    {
        /// <summary>Byte stride of a single PackedMeshVertex.</summary>
        private static readonly int s_vertexStride = Marshal.SizeOf<PackedMeshVertex>();

        /// <summary>Maximum arena capacity in bytes (512 MB).</summary>
        private const long MaxArenaBytes = 512L * 1024 * 1024;

        /// <summary>Live arenas for this layer, ordered by creation time.</summary>
        private readonly List<BufferArena> _arenas = new();

        /// <summary>Per-arena per-chunk indirect args buffers, parallel to _arenas.</summary>
        private readonly List<GraphicsBuffer> _perArenaArgsBuffers = new();

        /// <summary>Maps chunk coord to (arenaIndex, slotExists) for fast arena lookup.</summary>
        private readonly Dictionary<int3, int> _coordToArenaIndex = new();

        /// <summary>
        ///     Cached single-element array for per-slot args upload, avoiding per-call allocation.
        /// </summary>
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _slotArgsUpload =
            new GraphicsBuffer.IndirectDrawIndexedArgs[1];

        /// <summary>Debug/log label identifying this pool (e.g. "Opaque").</summary>
        private readonly string _name;

        /// <summary>GPU buffer resize service for growing per-chunk args buffers.</summary>
        private readonly GpuBufferResizer _resizer;

        /// <summary>Pipeline stats sink.</summary>
        private readonly IPipelineStats _pipelineStats;

        /// <summary>Maximum vertex elements per arena (derived from platform limit).</summary>
        private readonly int _arenaVertexCap;

        /// <summary>Maximum index elements per arena (derived from platform limit).</summary>
        private readonly int _arenaIndexCap;

        /// <summary>Initial vertex capacity for new arenas (estimated from render distance).</summary>
        private readonly int _initialVertexCapacity;

        /// <summary>Initial index capacity for new arenas (estimated from render distance).</summary>
        private readonly int _initialIndexCapacity;

        /// <summary>Current maximum number of per-chunk draw slots across all arenas.</summary>
        private int _maxChunkSlots;

        /// <summary>
        ///     Creates a BufferArenaPool for one render layer. The first arena is created
        ///     immediately with the estimated initial capacity. Additional arenas are created
        ///     on demand when existing arenas cannot satisfy an allocation.
        /// </summary>
        public BufferArenaPool(
            string poolName,
            int initialVertexCapacity, int initialIndexCapacity,
            int maxChunkSlots,
            GpuBufferResizer resizer, IPipelineStats pipelineStats)
        {
            _name = poolName;
            _resizer = resizer;
            _pipelineStats = pipelineStats;
            _maxChunkSlots = maxChunkSlots;
            _initialVertexCapacity = initialVertexCapacity;
            _initialIndexCapacity = initialIndexCapacity;

            // Derive arena cap from platform limit
            long platformMax = SystemInfo.maxGraphicsBufferSize;
            long arenaByteLimit = math.min(platformMax / 4, MaxArenaBytes);

            _arenaVertexCap = (int)(arenaByteLimit / s_vertexStride);
            _arenaIndexCap = (int)(arenaByteLimit / sizeof(int));

            // Ensure initial capacity does not exceed arena cap
            _initialVertexCapacity = math.min(_initialVertexCapacity, _arenaVertexCap);
            _initialIndexCapacity = math.min(_initialIndexCapacity, _arenaIndexCap);

            // Create the first arena
            CreateArena();
        }

        /// <summary>Number of live arenas in this pool.</summary>
        public int ArenaCount
        {
            get { return _arenas.Count; }
        }

        /// <summary>Returns true if any arena has geometry.</summary>
        public bool HasGeometry
        {
            get
            {
                for (int i = 0; i < _arenas.Count; i++)
                {
                    if (_arenas[i].HasGeometry)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Maximum number of per-chunk draw slots.</summary>
        public int MaxChunkSlots
        {
            get { return _maxChunkSlots; }
        }

        /// <summary>Total vertex capacity across all arenas (sum of arena capacities).</summary>
        public int VertexCapacity
        {
            get
            {
                int total = 0;

                for (int i = 0; i < _arenas.Count; i++)
                {
                    total += _arenas[i].VertexCapacity;
                }

                return total;
            }
        }

        /// <summary>Total index capacity across all arenas (sum of arena capacities).</summary>
        public int IndexCapacity
        {
            get
            {
                int total = 0;

                for (int i = 0; i < _arenas.Count; i++)
                {
                    total += _arenas[i].IndexCapacity;
                }

                return total;
            }
        }

        /// <summary>Total used vertex elements across all arenas.</summary>
        public int UsedVertices
        {
            get
            {
                int total = 0;

                for (int i = 0; i < _arenas.Count; i++)
                {
                    total += _arenas[i].VertexCapacity - _arenas[i].FreeVertexElements;
                }

                return total;
            }
        }

        /// <summary>
        ///     Allocates or updates a chunk slot across the arena list.
        ///     Tries the arena that already holds this coord first, then scans all arenas,
        ///     then creates a new arena if all are full.
        /// </summary>
        public void AllocateOrUpdate(
            int3 coord,
            NativeList<PackedMeshVertex> vertices, NativeList<int> indices)
        {
            int vertCount = vertices.Length;

            // Empty mesh — free existing slot if any
            if (vertCount == 0)
            {
                Free(coord);
                return;
            }

            // Fast path: try the arena that already holds this chunk
            if (_coordToArenaIndex.TryGetValue(coord, out int existingArenaIdx))
            {
                if (_arenas[existingArenaIdx].AllocateOrUpdate(coord, vertices, indices))
                {
                    return;
                }

                // Arena is full for the new size — free old slot, try elsewhere
                _arenas[existingArenaIdx].Free(coord);
                _coordToArenaIndex.Remove(coord);
            }

            // Try each existing arena
            for (int i = 0; i < _arenas.Count; i++)
            {
                if (_arenas[i].AllocateOrUpdate(coord, vertices, indices))
                {
                    _coordToArenaIndex[coord] = i;
                    return;
                }
            }

            // All arenas full — create a new one
            int newArenaIdx = CreateArena();

            if (!_arenas[newArenaIdx].AllocateOrUpdate(coord, vertices, indices))
            {
                // Should not happen in a fresh arena unless the mesh is larger than arena cap
                UnityEngine.Debug.LogError(
                    $"[BufferArenaPool] {_name}: Failed to allocate chunk {coord} " +
                    $"({vertCount} verts) in a fresh arena. Mesh exceeds arena capacity.");
                return;
            }

            _coordToArenaIndex[coord] = newArenaIdx;
        }

        /// <summary>Frees the slot for the given coord in whichever arena owns it.</summary>
        public void Free(int3 coord)
        {
            if (_coordToArenaIndex.TryGetValue(coord, out int arenaIdx))
            {
                _arenas[arenaIdx].Free(coord);
                _coordToArenaIndex.Remove(coord);
            }
        }

        /// <summary>Flushes dirty CPU mirror ranges to GPU for all arenas.</summary>
        public void FlushDirtyToGpu()
        {
            for (int i = 0; i < _arenas.Count; i++)
            {
                _arenas[i].FlushDirtyToGpu();
            }
        }

        /// <summary>
        ///     Updates the per-chunk indirect args entry for the given cull slot and chunk coord.
        ///     Zeroes ALL arenas' args at this cull slot first to clear any stale data from a
        ///     previous arena (a chunk may have migrated between arenas during AllocateOrUpdate),
        ///     then writes the live draw args to the arena that currently owns the chunk.
        ///     Called by ChunkMeshStore after AllocateOrUpdate.
        /// </summary>
        public void UpdatePerChunkArgs(int cullSlotId, int3 coord)
        {
            if (cullSlotId < 0 || cullSlotId >= _maxChunkSlots)
            {
                return;
            }

            // Zero all arenas first — prevents stale draw data when a chunk migrates
            ZeroPerChunkArgs(cullSlotId);

            if (_coordToArenaIndex.TryGetValue(coord, out int arenaIdx))
            {
                BufferArena arena = _arenas[arenaIdx];

                if (arena.TryGetSlot(coord, out ArenaSlot slot))
                {
                    _slotArgsUpload[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
                    {
                        indexCountPerInstance = (uint)slot.IndexCount,
                        instanceCount = 1,
                        startIndex = (uint)slot.IndexOffset,
                        baseVertexIndex = 0,
                        startInstance = 0,
                    };
                    _perArenaArgsBuffers[arenaIdx].SetData(_slotArgsUpload, 0, cullSlotId, 1);
                }
            }
        }

        /// <summary>
        ///     Zeroes the per-chunk indirect args entry at the given cull slot across all arenas.
        ///     Called when a chunk is being destroyed or evicted.
        /// </summary>
        public void ZeroPerChunkArgs(int cullSlotId)
        {
            if (cullSlotId < 0 || cullSlotId >= _maxChunkSlots)
            {
                return;
            }

            _slotArgsUpload[0] = default;

            for (int i = 0; i < _perArenaArgsBuffers.Count; i++)
            {
                _perArenaArgsBuffers[i].SetData(_slotArgsUpload, 0, cullSlotId, 1);
            }
        }

        /// <summary>
        ///     Grows the per-chunk args buffer capacity across all arenas.
        ///     Called by ChunkMeshStore when the cull slot pool is exhausted.
        /// </summary>
        public void GrowSlots(int newMaxSlots)
        {
            if (newMaxSlots <= _maxChunkSlots)
            {
                return;
            }

            for (int i = 0; i < _perArenaArgsBuffers.Count; i++)
            {
                _perArenaArgsBuffers[i] = _resizer.Resize(
                    _perArenaArgsBuffers[i],
                    newMaxSlots,
                    _maxChunkSlots,
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size);
            }

            _maxChunkSlots = newMaxSlots;
        }

        /// <summary>
        ///     Returns the draw batch info for the given arena index.
        ///     Used by ChunkMeshStore's multi-arena draw loop.
        /// </summary>
        public ArenaDrawBatch GetDrawBatch(int arenaIndex, int commandCount)
        {
            BufferArena arena = _arenas[arenaIndex];

            return new ArenaDrawBatch(
                _perArenaArgsBuffers[arenaIndex],
                arena.VertexBuffer,
                arena.IndexBuffer,
                commandCount,
                arena.HasGeometry);
        }

        /// <summary>
        ///     Returns all chunk coordinates currently stored in this pool.
        ///     Used by eviction logic to enumerate candidates.
        /// </summary>
        public Dictionary<int3, int>.KeyCollection ChunkCoords
        {
            get { return _coordToArenaIndex.Keys; }
        }

        /// <summary>Releases all arenas, args buffers, and clears all dictionaries.</summary>
        public void Dispose()
        {
            for (int i = 0; i < _arenas.Count; i++)
            {
                _arenas[i].Dispose();
            }

            _arenas.Clear();

            for (int i = 0; i < _perArenaArgsBuffers.Count; i++)
            {
                _perArenaArgsBuffers[i]?.Dispose();
            }

            _perArenaArgsBuffers.Clear();
            _coordToArenaIndex.Clear();
        }

        /// <summary>
        ///     Creates a new arena and its parallel args buffer. Returns the arena's index.
        /// </summary>
        private int CreateArena()
        {
            int arenaIdx = _arenas.Count;
            string arenaName = $"{_name}_Arena{arenaIdx}";

            BufferArena arena = new(
                arenaName,
                _initialVertexCapacity, _initialIndexCapacity,
                _arenaVertexCap, _arenaIndexCap,
                _resizer, _pipelineStats);

            _arenas.Add(arena);

            // Create per-chunk args buffer for this arena
            GraphicsBuffer argsBuffer = new(
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                _maxChunkSlots,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);

            // Zero-initialize all per-chunk args slots
            GraphicsBuffer.IndirectDrawIndexedArgs[] zeroArgs =
                new GraphicsBuffer.IndirectDrawIndexedArgs[_maxChunkSlots];
            argsBuffer.SetData(zeroArgs);

            _perArenaArgsBuffers.Add(argsBuffer);

#if LITHFORGE_DEBUG
            UnityEngine.Debug.Log(
                $"[BufferArenaPool] {_name}: Created arena {arenaIdx} " +
                $"(verts: {_initialVertexCapacity}, idx: {_initialIndexCapacity}, " +
                $"max verts: {_arenaVertexCap}, max idx: {_arenaIndexCap})");
#endif

            return arenaIdx;
        }
    }
}
