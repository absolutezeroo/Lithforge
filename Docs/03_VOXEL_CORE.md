# Lithforge — Voxel Core Technical Specification

## Chunk Design

### Dimensions

**32×32×32** chunks (32,768 voxels). Chosen for optimal greedy meshing alignment with 32-bit integers and a good balance between rebuild cost and spatial efficiency.

### ChunkData Storage (NativeArray)

Each chunk stores a flat `NativeArray<StateId>` allocated from the `ChunkPool` with `Allocator.Persistent`.

```csharp
/// <summary>
/// Core chunk storage. Owns a NativeArray of StateIds.
/// Allocated from ChunkPool, returned on unload.
/// Thread safety: only ONE writer at a time (generation job or main thread block change).
/// Multiple readers allowed when chunk is in READY state.
/// </summary>
public struct ChunkData : System.IDisposable
{
    public NativeArray<StateId> States;  // length = ChunkConstants.Volume (32768)

    /// <summary>
    /// Y-major index: y * 1024 + z * 32 + x
    /// Groups horizontal slices for cache-friendly meshing and sunlight propagation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetIndex(int x, int y, int z)
    {
        return (y * ChunkConstants.Size * ChunkConstants.Size)
             + (z * ChunkConstants.Size) + x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public StateId GetState(int x, int y, int z)
    {
        return States[GetIndex(x, y, z)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetState(int x, int y, int z, StateId state)
    {
        States[GetIndex(x, y, z)] = state;
    }

    public void Dispose()
    {
        if (States.IsCreated) States.Dispose();
    }
}
```

### Palette Compression

For serialization and memory reduction of homogeneous chunks, palette compression maps the full StateId space to a compact local palette:

```
1 type  → single value stored, no array needed (4 bytes total)
2 types → 1 bit per voxel = 4 KB + palette
3-4     → 2 bits = 8 KB + palette
5-16    → 4 bits = 16 KB + palette
17-256  → 8 bits = 32 KB + palette
257+    → 16 bits = 64 KB (no palette, raw StateId array)
```

Palette compression is applied:
- **On save**: ChunkSerializer compresses to palette format before writing to disk.
- **In memory (optional)**: For chunks far from the player that are loaded but not actively meshed, a compressed representation can reduce memory by 4-8x. Decompressed back to dense NativeArray when needed for meshing.
- **At runtime**: Chunks near the player always use dense NativeArray for O(1) random access in Burst jobs.

### ChunkConstants

```csharp
public static class ChunkConstants
{
    public const int Size = 32;
    public const int SizeSquared = Size * Size;        // 1024
    public const int Volume = Size * Size * Size;      // 32768
    public const int SizeBits = 5;                     // log2(32)
    public const int SizeMask = Size - 1;              // 0x1F, for bitwise modulo
}
```

### ChunkState Lifecycle

```
UNLOADED ──► GENERATING ──► GENERATED ──► MESHING ──► READY ◄──► DIRTY
                                                        │
                                                        ▼
                                                    UNLOADING ──► UNLOADED
```

| State | Meaning | Thread Access |
|-------|---------|---------------|
| `Unloaded` | No data allocated | — |
| `Generating` | Generation job in flight | Worker thread (exclusive write) |
| `Generated` | Data ready, waiting for neighbors before meshing | Main thread read, no writes |
| `Meshing` | Mesh job in flight | Worker thread (read-only on chunk data) |
| `Ready` | Rendered, stable | Main thread (write on block change), worker (read for neighbor meshing) |
| `Dirty` | Block changed, queued for remesh | Main thread (write), worker (read for remesh job) |
| `Unloading` | Pending save, then return to pool | Main thread only |

**Transition rules:**
- State transitions use `Interlocked.CompareExchange` for thread safety.
- A chunk cannot be meshed until all 6 face-adjacent neighbors are at least `Generated`.
- A chunk cannot be unloaded while any job holds a reference to its NativeArray. The `ChunkManager` must `Complete()` any pending job before unloading.

---

## BlockState System

### StateId

```csharp
/// <summary>
/// Index into the global StateRegistry. Blittable, Burst-compatible.
/// Value 0 is always AIR.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct StateId : System.IEquatable<StateId>
{
    public readonly ushort Value;

    public StateId(ushort value) { Value = value; }

    public static readonly StateId Air = new StateId(0);

    public bool Equals(StateId other) => Value == other.Value;
    public override int GetHashCode() => Value;
}
```

### StateRegistry (Managed — Tier 1/2)

Built during content loading. Computes the cartesian product of all block properties:

```
stone: no properties → 1 state → StateId[0] (but 0 is air, so stone starts at [1])
oak_log: axis ∈ {x,y,z} → 3 states → StateId[2..4]
furnace: facing ∈ {N,S,E,W} × lit ∈ {T,F} → 8 states → StateId[5..12]
oak_fence: N×E×S×W×waterlogged (2^5) → 32 states → StateId[13..44]
```

After all definitions are loaded, the registry is **frozen** (no further additions). Then:

```csharp
// Bake to NativeArray for Burst job access
NativeStateRegistry nativeRegistry = stateRegistry.BakeNative(Allocator.Persistent);
```

### NativeStateRegistry (Tier 2 — Burst-Compatible)

```csharp
/// <summary>
/// Read-only Burst-compatible view of the state registry.
/// Created once at content load freeze. Disposed at shutdown.
/// Passed as [ReadOnly] input to all Burst jobs that need block info.
/// </summary>
public struct NativeStateRegistry : System.IDisposable
{
    [ReadOnly] public NativeArray<BlockStateCompact> States;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BlockStateCompact GetState(StateId id)
    {
        return States[id.Value];
    }

    public void Dispose()
    {
        if (States.IsCreated) States.Dispose();
    }
}
```

### BlockStateCompact (Blittable)

```csharp
/// <summary>
/// Pre-resolved, cached block state data for Burst jobs.
/// All rendering/physics-relevant flags are pre-computed at content load.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct BlockStateCompact
{
    public ushort BlockId;          // index into block definition array
    public byte Flags;              // bit 0: isOpaque, 1: isFullCube, 2: isAir, 3: emitsLight
    public byte RenderLayer;        // 0=opaque, 1=cutout, 2=translucent
    public byte LightEmission;      // 0-15
    public byte LightFilter;        // 0-15 (how much light absorbed)
    public byte CollisionShape;     // CollisionShapeType enum value
    public byte TextureIndexBase;   // base index into Texture2DArray (face offsets added per-face)

    // Per-face texture indices (offset from TextureIndexBase or absolute)
    public ushort TexNorth;
    public ushort TexSouth;
    public ushort TexEast;
    public ushort TexWest;
    public ushort TexUp;
    public ushort TexDown;

    public bool IsOpaque => (Flags & 1) != 0;
    public bool IsFullCube => (Flags & 2) != 0;
    public bool IsAir => (Flags & 4) != 0;
    public bool EmitsLight => (Flags & 8) != 0;
}
```

---

## Chunk Neighbor Data

For meshing, each chunk needs to know the border blocks of its 6 neighbors (for face culling at chunk edges). Rather than passing the entire neighbor chunk, we extract 32×32 border slices:

```csharp
/// <summary>
/// Pre-extracted border slices from 6 neighbors.
/// Each slice is 32×32 = 1024 StateIds.
/// Created on main thread before scheduling mesh job.
/// Allocated with Allocator.TempJob, disposed after mesh job completes.
/// </summary>
public struct ChunkNeighborData : System.IDisposable
{
    public NativeArray<StateId> North;   // 1024 elements: neighbor's z=0 face
    public NativeArray<StateId> South;   // 1024 elements: neighbor's z=31 face
    public NativeArray<StateId> East;    // 1024 elements: neighbor's x=0 face
    public NativeArray<StateId> West;    // 1024 elements: neighbor's x=31 face
    public NativeArray<StateId> Up;      // 1024 elements: neighbor's y=0 face
    public NativeArray<StateId> Down;    // 1024 elements: neighbor's y=31 face

    public static ChunkNeighborData Allocate()
    {
        return new ChunkNeighborData
        {
            North = new NativeArray<StateId>(1024, Allocator.TempJob),
            South = new NativeArray<StateId>(1024, Allocator.TempJob),
            East  = new NativeArray<StateId>(1024, Allocator.TempJob),
            West  = new NativeArray<StateId>(1024, Allocator.TempJob),
            Up    = new NativeArray<StateId>(1024, Allocator.TempJob),
            Down  = new NativeArray<StateId>(1024, Allocator.TempJob),
        };
    }

    public void Dispose()
    {
        if (North.IsCreated) North.Dispose();
        if (South.IsCreated) South.Dispose();
        if (East.IsCreated)  East.Dispose();
        if (West.IsCreated)  West.Dispose();
        if (Up.IsCreated)    Up.Dispose();
        if (Down.IsCreated)  Down.Dispose();
    }
}
```

---

## World Storage

### Region File Format

Identical to engine-agnostic spec. One region file covers 32×32 chunk columns.

```
File: r.{rx}.{rz}.lfrg

Header (8 KB):
  [4 bytes] Magic: "LFRG"
  [2 bytes] Version: u16
  [2 bytes] Reserved
  [1024 × 8 bytes] Chunk column index (offset_u32, size_u24, compression_u8)

Body (variable):
  Chunk column data (zstd compressed):
    Column header: y_min, y_max, flags
    Per vertical section:
      section_y: i8
      palette_size: u16
      palette: StateId[palette_size]
      bits_per_entry: u8
      block_data: packed bits (32768 entries)
      light_data: packed nibbles (32768 entries)
      crc32: u32
    Metadata entries (block entities, if any)
```

### Version and Migration

- Format version is per-region-file.
- Engine refuses to load regions with a higher version than it supports (no forward compat).
- Engine applies migration transforms for older versions (sequential: v1→v2→v3→current).
- When a mod is removed, its StateIds are unknown. Unknown StateIds are replaced with air on chunk load, logged as warnings.
- World metadata (`world.json`) stores: engine version, world seed, content hash (SHA256 of all registered block/item IDs sorted), creation timestamp.

---

## Chunk Management

### ChunkManager Responsibilities

```csharp
public sealed class ChunkManager : System.IDisposable
{
    // Loaded chunks indexed by coordinate
    private readonly Dictionary<int3, Chunk> _loadedChunks;

    // Pool for NativeArray recycling
    private readonly ChunkPool _chunkPool;

    // Job tracking (must complete before unloading chunk)
    private readonly Dictionary<int3, JobHandle> _pendingJobs;

    // Queues
    private readonly PriorityQueue<int3, float> _loadQueue;      // distance-prioritized
    private readonly Queue<int3> _unloadQueue;
    private readonly Queue<GenerationResult> _generationResults;  // from completed jobs
    private readonly Queue<MeshResult> _meshResults;              // from completed jobs

    /// <summary>
    /// Called each frame from GameLoop.Update().
    /// Orchestrates the entire chunk lifecycle.
    /// </summary>
    public void Update(float3 playerPosition, int renderDistance)
    {
        // 1. Determine desired loaded set (spiral from player)
        // 2. Queue missing chunks for generation
        // 3. Queue distant chunks for unloading (complete pending jobs first)
        // 4. Process completed generation results
        // 5. For newly generated chunks: check if all neighbors ready → queue meshing
        // 6. Process completed mesh results → pass to ChunkRenderManager
        // 7. Save dirty chunks periodically
    }
}
```

### Dirty Chunk Propagation

When a block changes at a chunk border, the adjacent chunk is also marked dirty:

```
Block at local (0, y, z) → also dirty chunk (cx-1, cy, cz)
Block at local (31, y, z) → also dirty chunk (cx+1, cy, cz)
Block at local (x, 0, z) → also dirty chunk (cx, cy-1, cz)
Block at local (x, 31, z) → also dirty chunk (cx, cy+1, cz)
Block at local (x, y, 0) → also dirty chunk (cx, cy, cz-1)
Block at local (x, y, 31) → also dirty chunk (cx, cy, cz+1)
```

If a remesh job is already in flight for a chunk that becomes dirty again, the in-flight job's result is **discarded** when it completes (stale flag), and a new remesh is scheduled with current data.

---

## Invariants

1. `StateId(0)` is always AIR. This is hardcoded and cannot be overridden.
2. A chunk's `NativeArray<StateId>` is always exactly `ChunkConstants.Volume` (32,768) elements when allocated.
3. A chunk in state `Ready` has either a valid rendered mesh or an explicit empty-mesh marker (all-air chunk).
4. A chunk cannot transition `Unloaded → Meshing` — must pass through `Generating → Generated`.
5. A chunk's NativeArray is allocated from `ChunkPool` (Allocator.Persistent) and returned to the pool on unload — never disposed individually.
6. No chunk may be unloaded while a `JobHandle` referencing its data is incomplete.
7. The `StateRegistry` is frozen before any chunk is generated. No StateIds are added after freeze.
8. The `NativeStateRegistry` bake has exactly the same length and indexing as the managed `StateRegistry`.
9. Light data is valid only after `InitialLightingJob` has completed for that chunk.
10. Generation output is deterministic given the same world seed and content definitions.
