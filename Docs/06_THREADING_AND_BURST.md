# Lithforge — Threading, Burst & Native Data

## Threading Model

Lithforge uses **Unity's Job System** exclusively. No custom thread pool, no `System.Threading.Thread`, no manual thread management.

### Thread Responsibilities

```
┌──────────────────────────────────────────────────────────┐
│                    MAIN THREAD                            │
│  Unity's Update() / LateUpdate()                          │
│                                                           │
│  • Poll job completion (JobHandle.IsCompleted)            │
│  • Upload MeshData → Mesh (Mesh.ApplyAndDisposeWritable)  │
│  • Create/destroy chunk GameObjects                       │
│  • Update entity visual transforms                        │
│  • Process input events                                   │
│  • Update UI                                              │
│  • Schedule next frame's jobs                             │
│  • Dispose consumed NativeContainers                      │
│                                                           │
│  Budget: ≤ 4ms per frame at 60 FPS                        │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│                 UNITY WORKER THREADS                       │
│  Managed by Unity's JobScheduler (no manual control)      │
│                                                           │
│  • Chunk generation (TerrainShapeJob, CaveCarverJob, etc) │
│  • Chunk meshing (GreedyMeshJob)                          │
│  • Light propagation (LightPropagationJob)                │
│  • LOD mesh generation (LODMeshJob)                       │
│  • Entity movement/AI (DOTS systems via IJobChunk)        │
│                                                           │
│  Rules:                                                   │
│  • All jobs implement IJob, IJobFor, or IJobParallelFor   │
│  • All jobs are [BurstCompile] structs                    │
│  • All data via NativeContainers                          │
│  • No UnityEngine API calls                               │
│  • No managed heap allocation                             │
└──────────────────────────────────────────────────────────┘
```

---

## Burst Compilation Rules

### What Gets Burst-Compiled

| Job | Input NativeContainers | Output NativeContainers | Allocator |
|-----|----------------------|------------------------|-----------|
| `TerrainShapeJob` | NoiseConfig (blittable struct), seed | `NativeArray<StateId>` (chunk), `NativeArray<int>` (heightmap) | TempJob |
| `CaveCarverJob` | `NativeArray<StateId>` (chunk), NoiseConfig | Modifies chunk in-place | — |
| `BiomeAssignmentJob` | `NativeArray<int>` (heightmap), NoiseConfig, `NativeArray<NativeBiomeData>` | `NativeArray<byte>` (biome map) | TempJob |
| `SurfaceBuilderJob` | Chunk, heightmap, biome map, `NativeArray<NativeBiomeData>` | Modifies chunk in-place | — |
| `OreGenerationJob` | Chunk, `NativeArray<OreConfig>`, seed | Modifies chunk in-place | — |
| `InitialLightingJob` | Chunk, `NativeStateRegistry` | `NativeArray<byte>` (light data) | TempJob |
| `GreedyMeshJob` | Chunk, 6× neighbor borders, `NativeStateRegistry`, `NativeAtlasLookup` | `NativeList<MeshVertex>`, `NativeList<int>` (indices) | TempJob |
| `LODMeshJob` | Chunk (downsampled), `NativeStateRegistry` | `NativeList<MeshVertex>`, `NativeList<int>` | TempJob |
| `LightPropagationJob` | Chunk, light data, `NativeQueue<int3>` (BFS queue) | Modifies light data in-place | TempJob (queue) |
| `VoxelRaycast` | Start pos, direction, `NativeArray<StateId>`, `NativeStateRegistry` | Hit result (blittable struct) | — |
| DOTS `MovementSystem` | Entity velocity, position, chunk access | Modifies position | — |

### What Does NOT Get Burst-Compiled

| System | Reason |
|--------|--------|
| Content loading (JSON parsing) | String-heavy, one-time cost, managed types needed |
| Registry building | Dictionary/List operations, one-time at load |
| StateRegistry construction | Cartesian product with managed property types |
| Model resolution (parent inheritance) | String lookups, tree traversal |
| Texture atlas building | Managed Texture2D API |
| EventBus | Delegate dispatch, managed subscribers |
| Mod loading | Filesystem access, JSON parsing, reflection |
| UI updates | Unity UI API is main-thread managed |
| Mesh upload to GPU | UnityEngine.Mesh API (main thread, managed) |
| Save/Load I/O | File streams, zstd compression via managed interop |
| Network packet processing | Managed byte[] buffers, protocol logic |

---

## Blittable Type Requirements

For Burst compatibility, all types used in jobs must be **blittable** (no managed references).

### Dual Representation Pattern

Types that exist in both managed (for content loading, modding API) and unmanaged (for Burst jobs) forms:

| Managed Type (Tier 1) | Blittable Type (Tier 2) | Baked When |
|----------------------|------------------------|-----------|
| `BlockState` (class, Dictionary properties) | `BlockStateCompact` (struct, cached flags) | Content load freeze |
| `Registry<BlockDefinition>` | `NativeStateRegistry` (NativeArray) | Content load freeze |
| `BiomeDefinition` (class, string refs) | `NativeBiomeData` (struct, IDs only) | Content load freeze |
| `AtlasRegion` per texture name | `NativeAtlasLookup` (NativeArray indexed by texture ID) | Atlas build |
| `OreDefinition` (class) | `NativeOreConfig` (struct) | Content load freeze |

### BlockStateCompact — The Key Blittable Type

```csharp
/// <summary>
/// Burst-compatible cached block state data.
/// Pre-resolved at content load time, indexed by StateId.
/// This is what meshing/worldgen/lighting jobs actually read.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct BlockStateCompact
{
    public ushort BlockId;          // index into block definition array
    public byte Flags;              // bit 0: isOpaque, bit 1: isFullCube, bit 2: isAir
    public byte RenderLayer;        // 0=opaque, 1=cutout, 2=translucent
    public byte LightEmission;      // 0-15
    public byte LightFilter;        // 0-15
    public byte CollisionShape;     // enum index
    public byte Reserved;           // alignment padding

    public bool IsOpaque => (Flags & 1) != 0;
    public bool IsFullCube => (Flags & 2) != 0;
    public bool IsAir => (Flags & 4) != 0;
}

/// <summary>
/// NativeArray wrapper for Burst job access to the state registry.
/// Created once at content load, disposed at shutdown.
/// </summary>
public struct NativeStateRegistry : System.IDisposable
{
    [ReadOnly] public NativeArray<BlockStateCompact> States;

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

### MeshVertex — Blittable, Matches Unity VertexAttributes

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct MeshVertex
{
    public float3 Position;          // 12 bytes
    public float3 Normal;            // 12 bytes
    public float2 UV;                // 8 bytes (atlas coordinates)
    public half4 Color;              // 8 bytes (r=AO, g=blockLight, b=sunLight, a=tintIndex)
    // Total: 40 bytes per vertex
}

// Matching VertexAttributeDescriptor array:
public static readonly VertexAttributeDescriptor[] VertexAttributes = new[]
{
    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
    new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float16, 4),
};
```

---

## NativeContainer Ownership Rules

Every NativeContainer has exactly ONE owner responsible for `Dispose()`.

### Persistent Containers (Allocator.Persistent)

| Container | Owner | Created | Disposed | Lifetime |
|-----------|-------|---------|----------|----------|
| `ChunkData.States` (NativeArray) | `ChunkPool` | When chunk allocated from pool | When chunk returned to pool, or pool shutdown | World lifetime |
| `ChunkData.LightData` (NativeArray) | `Chunk` | After initial lighting | When chunk unloaded | Chunk lifetime |
| `NativeStateRegistry.States` | `ContentManager` | After content load freeze | At shutdown | Application lifetime |
| `NativeAtlasLookup.Regions` | `ContentManager` | After atlas build | At shutdown | Application lifetime |
| `NativeBiomeData[]` | `ContentManager` | After content load | At shutdown | Application lifetime |

### Per-Job Containers (Allocator.TempJob)

| Container | Owner | Created | Disposed | Lifetime |
|-----------|-------|---------|----------|----------|
| `GreedyMeshJob` output lists | Main thread | Before job schedule | After mesh upload | One frame |
| `GenerationContext` temp arrays | Pipeline orchestrator | Before stage job | After stage completes | One job |
| `LightPropagationJob` queue | LightEngine | Before job schedule | After job completes | One job |
| Heightmap, BiomeMap (per-gen) | Pipeline orchestrator | Before pipeline | After pipeline completes | One generation |

### ChunkPool — Avoiding Allocation Churn

```csharp
/// <summary>
/// Pre-allocates NativeArrays for chunks to avoid per-chunk Allocator.Persistent calls.
/// Chunks are checked out when needed and returned when unloaded.
/// </summary>
public sealed class ChunkPool : System.IDisposable
{
    private readonly Stack<NativeArray<StateId>> _available;
    private readonly int _capacity;

    public ChunkPool(int initialCapacity)
    {
        _capacity = initialCapacity;
        _available = new Stack<NativeArray<StateId>>(initialCapacity);
        for (int i = 0; i < initialCapacity; i++)
        {
            _available.Push(new NativeArray<StateId>(
                ChunkConstants.Volume, Allocator.Persistent, NativeArrayOptions.ClearMemory));
        }
    }

    public NativeArray<StateId> Checkout()
    {
        if (_available.Count > 0) return _available.Pop();
        // Pool exhausted: allocate new (log warning)
        return new NativeArray<StateId>(ChunkConstants.Volume, Allocator.Persistent);
    }

    public void Return(NativeArray<StateId> array)
    {
        // Clear and return to pool
        NativeArray<StateId>.Copy(/* zero-filled source */, array);
        _available.Push(array);
    }

    public void Dispose()
    {
        while (_available.Count > 0) _available.Pop().Dispose();
    }
}
```

---

## Job Scheduling — Frame Flow

```csharp
// GameLoop.cs — MonoBehaviour driving the main loop
public sealed class GameLoop : MonoBehaviour
{
    private void Update()
    {
        // 1. Complete previous frame's jobs that are ready
        CompletePendingJobs();

        // 2. Process completed generation results
        ProcessGenerationResults(maxPerFrame: 8);

        // 3. Process completed mesh results → upload to GPU
        ProcessMeshResults(maxPerFrame: 4);

        // 4. Process completed light results → may trigger remesh
        ProcessLightResults();

        // 5. Update chunk loading (determine what to load/unload)
        _chunkManager.UpdateLoadingQueue(_playerPosition, _renderDistance);

        // 6. Schedule new generation jobs
        ScheduleGenerationJobs(maxPerFrame: 4);

        // 7. Schedule new meshing jobs (for chunks with all neighbors ready)
        ScheduleMeshingJobs(maxPerFrame: 4);

        // 8. Schedule light updates (for dirty chunks)
        ScheduleLightJobs(maxPerFrame: 2);
    }

    private void LateUpdate()
    {
        // Entity visual sync (DOTS → Transform sync handled by Unity.Transforms)
        // Camera updates
        // Debug overlay updates
    }

    private void OnDestroy()
    {
        // Complete ALL pending jobs (cannot dispose NativeContainers while jobs use them)
        CompleteAllPendingJobs();
        // Dispose all NativeContainers
        _contentManager.Dispose();
        _chunkPool.Dispose();
        // ... etc
    }
}
```

### Job Dependency Chains

For a single chunk, the dependency chain is:

```
TerrainShapeJob
    ↓ (JobHandle dependency)
CaveCarverJob
    ↓
BiomeAssignmentJob
    ↓
SurfaceBuilderJob
    ↓
OreGenerationJob
    ↓
InitialLightingJob
    ↓ (generation complete, wait for neighbor chunks)
GreedyMeshJob (requires 6 neighbor borders — scheduled when all neighbors ≥ GENERATED)
    ↓ (mesh complete)
Main thread: Mesh.ApplyAndDisposeWritableMeshData (not a job, runs in Update)
```

Each stage's job depends on the previous via `JobHandle`:

```csharp
public JobHandle ScheduleGeneration(ChunkCoord coord, long seed)
{
    // Allocate context NativeArrays
    NativeArray<StateId> chunkData = _chunkPool.Checkout();
    NativeArray<int> heightMap = new NativeArray<int>(1024, Allocator.TempJob);
    NativeArray<byte> biomeMap = new NativeArray<byte>(1024, Allocator.TempJob);

    JobHandle h1 = new TerrainShapeJob
    {
        ChunkData = chunkData,
        HeightMap = heightMap,
        Seed = seed,
        CoordX = coord.X, CoordY = coord.Y, CoordZ = coord.Z,
        NoiseConfig = _terrainNoiseConfig,
    }.Schedule();

    JobHandle h2 = new CaveCarverJob
    {
        ChunkData = chunkData,
        Seed = seed,
        CoordX = coord.X, CoordY = coord.Y, CoordZ = coord.Z,
        CaveConfig = _caveConfig,
    }.Schedule(h1); // depends on terrain

    JobHandle h3 = new BiomeAssignmentJob
    {
        HeightMap = heightMap,
        BiomeMap = biomeMap,
        BiomeData = _nativeBiomeData,
        Seed = seed,
        CoordX = coord.X, CoordZ = coord.Z,
    }.Schedule(h2);

    // ... chain continues ...

    return finalHandle;
}
```

---

## ECS Integration (Entities Only)

DOTS/ECS is used **exclusively** for entity simulation (mobs, NPCs, projectiles). It is NOT used for chunks, meshing, worldgen, lighting, content loading, or UI.

### Why Hybrid

| Use ECS For | Reason |
|-------------|--------|
| Mobs (100s) | SoA layout, batch processing, Burst systems |
| Projectiles (1000s) | Lightweight, batch lifetime/movement |
| Particles (optional) | If Unity's particle system is insufficient |

| Do NOT Use ECS For | Reason |
|--------------------|--------|
| Chunks | Spatial containers, not entities. ChunkManager is simpler. |
| Meshing | IJob on chunk data, not entity iteration |
| WorldGen | Pipeline of Burst jobs, not entity iteration |
| Lighting | BFS propagation, not per-entity |
| Crafting | Infrequent, UI-bound, managed types needed |
| Modding API | ECS types are not mod-friendly (no interfaces, archetypes are opaque) |
| Content loading | Managed types, one-time cost |

### Entity ↔ Voxel World Communication

DOTS entities need to query the voxel world (for collision, block access). This requires passing `NativeArray<StateId>` (chunk data) into DOTS systems:

```csharp
[BurstCompile]
public partial struct MovementSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Access voxel world data via singleton component
        VoxelWorldAccess worldAccess = SystemAPI.GetSingleton<VoxelWorldAccess>();

        new MovementJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            ChunkLookup = worldAccess.ChunkLookup,    // NativeHashMap<int3, ChunkRef>
            StateRegistry = worldAccess.StateRegistry,  // NativeStateRegistry
        }.ScheduleParallel();
    }
}
```

The `VoxelWorldAccess` singleton is updated each frame by the main thread with current chunk data pointers.

---

## Memory Budget

### Per-Chunk Memory (32³ = 32,768 voxels)

| Data | Size | Notes |
|------|------|-------|
| ChunkData (dense, no palette) | 64 KB | NativeArray<StateId> (32768 × 2 bytes) |
| ChunkData (palette, 5 types) | ~16 KB | 4 bits per voxel |
| Light data | 32 KB | NativeArray<byte> (32768 × 1 byte, packed 4+4) |
| Mesh data (typical opaque) | 10-50 KB | Varies wildly with terrain |
| Mesh data (cutout) | 0-10 KB | Only if vegetation present |
| Total per loaded chunk | ~80-160 KB | |

### System-Wide Budget (16-chunk render distance)

| Item | Count | Memory |
|------|-------|--------|
| Loaded chunks | ~10,000 | ~800 MB - 1.2 GB |
| Chunk pool overhead | 512 pre-allocated | ~32 MB |
| NativeStateRegistry | 1 | < 1 MB |
| NativeAtlasLookup | 1 | < 1 MB |
| Mesh objects (GPU) | ~10,000 | ~200-500 MB VRAM |
| **Total native memory** | | **~1-1.7 GB** |

This is within acceptable bounds for a modern PC. For lower-end hardware, reduce render distance (8 chunks ≈ ~200 MB).
