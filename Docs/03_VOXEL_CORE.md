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
UNLOADED → LOADING → GENERATING → DECORATING → RELIGHTPENDING → GENERATED → MESHING → READY
                                                                     │ (LOD>0)          ↑
                                                                     └── LODScheduler ───┘
```

| State | Ordinal | Meaning | Thread Access |
|-------|---------|---------|---------------|
| `Unloaded` | 0 | No data allocated | — |
| `Loading` | 1 | Being loaded from disk | Main thread |
| `Generating` | 2 | Generation job in flight | Worker thread (exclusive write) |
| `Decorating` | 3 | Decoration stage running (trees) | Main thread |
| `RelightPending` | 4 | Block edit occurred, must relight before meshing | Main thread |
| `Generated` | 5 | Data ready, first state eligible for meshing | Main thread read |
| `Meshing` | 6 | Mesh job in flight (GreedyMeshJob or LODGreedyMeshJob) | Worker thread (read-only) |
| `Ready` | 7 | Rendered, stable | Main thread (write on block change) |

**Transition rules:**
- States from `RelightPending` onward have valid voxel data. States from `Generated` onward are eligible for meshing.
- `Generated → Meshing` is handled by MeshScheduler (LOD0) or LODScheduler (LOD>0).
- When a Ready chunk's LOD level changes from >0 to 0, its state resets to `Generated` for full remeshing.
- A chunk cannot be meshed until all 6 face-adjacent neighbors are at least `Generated` (LOD0 meshing only).
- A chunk cannot be unloaded while any job holds a reference to its NativeArray.
- Dirty chunks on unload are saved via `WorldStorage.SaveChunk()` before returning to ChunkPool.

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

For meshing, each chunk needs to know the border blocks of its 6 neighbors (for face culling at chunk edges). Border slices are extracted via `ExtractSingleBorderJob` (Burst-compiled) on worker threads:

```csharp
/// <summary>
/// Burst-compiled job that extracts a single 32×32 border slice from a chunk.
/// One job per face direction, scheduled in parallel for all 6 neighbors.
/// </summary>
[BurstCompile]
public struct ExtractSingleBorderJob : IJob
{
    [ReadOnly] public NativeArray<StateId> ChunkData;
    public int FaceDirection;  // 0=+X, 1=-X, 2=+Y, 3=-Y, 4=+Z, 5=-Z
    [WriteOnly] public NativeArray<StateId> Output;  // 32×32 = 1024 elements
}
```

The 6 extracted borders are stored as fields in `GreedyMeshData` (e.g., `NeighborPosX`, `NeighborNegX`, etc.) and passed as `[ReadOnly]` inputs to `GreedyMeshJob`. Border extraction jobs are chained as dependencies of the mesh job.

A managed reference implementation exists in `ChunkBorderExtractor.ExtractBorder()` for comparison.

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
- World metadata (`world.json` in world directory, written by `WorldMetadata.Save()`) stores: seed, version, content_hash, last_saved (ISO 8601).

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
        // 6. Process completed mesh results → pass to ChunkMeshStore
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

If a remesh job is already in flight for a chunk that becomes dirty again, the edit is deferred.

### Deferred Edits

When a block change occurs on a chunk in `Meshing` state (mesh job in-flight), the edit cannot be applied immediately (the job is reading ChunkData). Instead, `ChunkManager.SetBlock()` adds a `DeferredEdit` to the chunk:

```csharp
public struct DeferredEdit
{
    public int FlatIndex;
    public StateId NewState;
}
```

After the mesh job completes in `MeshScheduler.PollCompleted()`:
1. The completed mesh is uploaded to GPU normally
2. Deferred edits are applied to `ChunkData`
3. Chunk state is set to `RelightPending` (light removal/reseed needed before re-meshing)

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
