# Lithforge — CLAUDE.md (AI Development Context)

## Project Identity

- **Name**: Lithforge
- **Type**: Open-source voxel game-creation platform
- **Engine**: Unity / C# with Burst, Jobs, NativeContainers, DOTS (entities only)
- **Render Pipeline**: URP (Universal Render Pipeline)
- **Architecture**: Three-tier (Pure C# → Unity Core → Unity Runtime)
- **Data convention**: ScriptableObjects in `Assets/Resources/Content/`, loaded via `Resources.LoadAll<T>()`
- **License**: LGPL-2.1-or-later

## Three-Tier Architecture

```
Tier 1 — Pure C# (no Unity dependency)
  Package: com.lithforge.core
  Contains: ResourceId, Registry<T>, ContentValidator, ILogger

Tier 2 — Unity Core (Unity.Collections, Unity.Mathematics, Unity.Burst, Unity.Jobs)
  Packages: com.lithforge.voxel, com.lithforge.worldgen, com.lithforge.meshing, com.lithforge.physics
  Contains: NativeArray chunk data, Burst-compiled jobs, NativeStateRegistry, ChunkManager,
            WorldStorage, CraftingEngine, LootResolver, Inventory
  NO UnityEngine references (except Unity.Mathematics, Unity.Collections, Unity.Burst, Unity.Jobs).

Tier 3 — Unity Runtime (UnityEngine, URP, InputSystem, UI Toolkit)
  Location: Assets/Lithforge.Runtime/
  Contains: GameLoop, Schedulers (GenerationScheduler, MeshScheduler, RelightScheduler, LODScheduler),
            Rendering (ChunkMeshStore, MegaMeshBuffer, HiZPyramid, BiomeTintManager, FrustumCull.compute),
            ContentPipeline, UI (ContainerScreen, PlayerInventoryScreen, HotbarDisplay, SettingsScreen,
            LoadingScreen), SkyController, SpawnManager, Bootstrap,
            Debug (FrameProfiler, PipelineStats, BenchmarkRunner)
```

**Planned but not yet implemented**: com.lithforge.crafting (separate package), com.lithforge.modding, com.lithforge.lighting (standalone), com.lithforge.entity (DOTS ECS). Currently, crafting/loot/inventory live in com.lithforge.voxel, and lighting jobs live in com.lithforge.worldgen.

## Critical Rules

### Code Conventions (All Tiers)

> Full naming conventions reference: **`Docs/CONVENTIONS.md`** (C#, Unity APIs, DOTS/ECS, assets, shaders, namespaces).

1. **No `var`** — explicit types everywhere, enforced by `.editorconfig` as error.
2. **Target-typed `new()`** — prefer `List<int> items = new();` over repeating the type. The left-hand side already declares the type.
3. **Object initializers** — prefer `new Foo { Bar = 1, Baz = 2 }` when it produces cleaner code than sequential assignment.
4. **Switch expressions** — prefer `x switch { ... }` over `switch` statements when the result is a direct assignment or return and the cases are simple.
5. **Pattern matching in `if`/loops** — prefer `if (x is int i)`, `is not null`, `is > 0` when it improves readability.
6. **Allman braces** — opening brace on new line, always.
7. **One file per type** — each class, interface, enum, struct gets its own file.
8. **Namespace = package path** — `namespace Lithforge.Voxel.Block;`
9. **Private fields: `_camelCase`** — underscore prefix.
10. **No expression-bodied methods** — block bodies only (switch expressions and pattern matching are not expression-bodied methods).
11. **Interfaces: `I` prefix** — `IRegistry`, `ILogger`.
12. **Accessibility always explicit** — `public`, `private`, `internal`.

### Tier 1 Rules

13. **Zero Unity references** — `Lithforge.Core.asmdef` must not reference any Unity assembly.
14. **Standard .NET types only** — `Dictionary`, `List`, `string`, etc.
15. **Unit-testable in isolation** — could compile against .NET Standard.

### Tier 2 Rules (Burst/Jobs)

16. **`[BurstCompile]` on all hot-path job structs.**
17. **`Unity.Mathematics` types** — `float3`, `int3`, `math.sin()` not `System.Math`.
18. **`NativeArray<T>`** not managed arrays for chunk data, mesh data, light data.
19. **No heap allocation in Burst paths** — no `new`, no boxing, no string ops.
20. **No try/catch in Burst paths.**
21. **No interface dispatch in Burst paths** — no virtual calls.
22. **No `class` references in Burst jobs** — only blittable structs.
23. **`[ReadOnly]` attribute on job inputs** that are not modified.
24. **`Allocator.TempJob`** for per-frame data, **`Allocator.Persistent`** for long-lived data.
25. **Every NativeContainer has a documented owner and dispose point.**
26. **ChunkPool** for NativeArray recycling — no per-chunk Persistent allocation.

### Tier 3 Rules

27. **GPU buffer upload on main thread only** — `MegaMeshBuffer.FlushDirtyToGpu()` writes vertex/index data to persistent `GraphicsBuffer`s. Drawing via `Graphics.RenderPrimitivesIndexedIndirect()`.
28. **No Burst code** in Tier 3 — Tier 3 consumes job results, does not produce them.
29. **Shaders are hand-written HLSL** (URP-compatible), not Shader Graph.

## Key Types

| Type | Tier | Blittable | Purpose |
|------|------|-----------|---------|
| `ResourceId` | 1 | No (string) | "namespace:name" identifier |
| `Registry<T>` | 1 | No (managed) | Frozen read-only registry |
| `BlockDefinition` | 3 | No (ScriptableObject) | Data-driven block properties |
| `StateId` | 2 | Yes (ushort) | Index into NativeStateRegistry |
| `BlockStateCompact` | 2 | Yes (struct) | Cached render/physics flags for Burst |
| `NativeStateRegistry` | 2 | Yes (NativeArray) | Burst-accessible state lookup |
| `ChunkData` | 2 | Yes (NativeArray) | 32³ voxel storage |
| `PackedMeshVertex` | 2 | Yes (struct) | 16-byte bit-packed vertex (4×uint32) |
| `ClimateData` | 2 | Yes (struct) | Per-column temperature/humidity/continentalness/erosion |
| `DeferredEdit` | 2 | Yes (struct) | Pending block edit (FlatIndex + NewState) |
| `MiningContext` | 2 | Yes (struct) | Mutable mining calculation state |
| `NativeNoiseConfig` | 2 | Yes (struct) | Noise parameters for Burst jobs |
| `NativeBiomeData` | 2 | Yes (struct) | Biome data for Burst jobs |
| `ChunkBoundsGPU` | 3 | Yes (struct) | Per-chunk AABB for GPU frustum culling (32 bytes) |
| `ChunkMeshStore` | 3 | No (IDisposable) | GPU-driven chunk mesh storage + indirect draw |
| `MegaMeshBuffer` | 3 | No (IDisposable) | Persistent GPU vertex/index buffers per render layer |
| `HiZPyramid` | 3 | No (IDisposable) | Hi-Z occlusion culling mipmap pyramid |
| `ContainerScreen` | 3 | No (MonoBehaviour) | Abstract base for all container UIs |
| `SlotInteractionController` | 3 | No (class) | Minecraft-style slot interaction (6 modes) |
| `FrameProfiler` | 3 | No (static) | Zero-alloc frame section profiler (14 sections) |
| `PipelineStats` | 3 | No (static) | Pipeline counters (per-frame + cumulative) |
| `FrameBudget` | 3 | Yes (struct) | Lightweight time-budget tracker for polling loops |

## Dual Representation Pattern

ScriptableObjects (managed, editor-friendly) are baked to Burst-compatible native structs at startup:

```
BlockDefinition (SO) → StateRegistry ──BakeNative()──► NativeStateRegistry (NativeArray<BlockStateCompact>)
BiomeDefinition (SO) ──BakeNative()──► NativeBiomeData (blittable)
OreDefinition (SO) ──BakeNative()──► NativeOreConfig (blittable)
Atlas regions ──BakeNative()──► NativeAtlasLookup (NativeArray)
```

Baking happens once during `ContentPipeline.Build()`. The managed SO is authoritative (source of truth); the native version is derived and read-only.

## Content System

Content is defined via **Unity ScriptableObjects** loaded through `Resources.LoadAll<T>()` at startup by `ContentPipeline.Build()`.

Location: `Assets/Resources/Content/`

```
Blocks/          → BlockDefinition (ScriptableObject)
BlockStates/     → BlockStateMapping (ScriptableObject)
Models/          → BlockModel (ScriptableObject)
Items/           → ItemDefinition (ScriptableObject)
ItemModels/      → BlockModel (ScriptableObject)
LootTables/      → LootTable (ScriptableObject)
Tags/            → Tag (ScriptableObject)
Recipes/         → RecipeDefinition (ScriptableObject)
Biomes/          → BiomeDefinition (ScriptableObject)
Ores/            → OreDefinition (ScriptableObject)
Textures/Blocks/ → Texture2D (block face textures)
Textures/Items/  → Texture2D (item icons)
```

Settings: `Assets/Resources/Settings/`

```
ChunkSettings.asset      → render distance, LOD distances, spawn radius, mesh budget
WorldGenSettings.asset   → noise configs (terrain, cave, temperature, humidity), sea level
RenderingSettings.asset  → fog, AO, sky colors
PhysicsSettings.asset    → gravity, step height
GameplaySettings.asset   → gameplay tuning
DebugSettings.asset      → debug overlay toggles
```

**Loading pipeline** (`ContentPipeline.Build()` is an `IEnumerable<string>` yielding phase descriptions):
1. Load BlockDefinitions → register in StateRegistry
2. Expand BlockStates (cartesian product of properties) → StateRegistry
2.5. Load BlockEntityDefinitions → patch StateRegistry with block entity type IDs
3. Resolve BlockModels (ContentModelResolver with parent inheritance chain)
4. Resolve per-state per-face Texture2D references
5. Resolve overlays and tints (per-face tint types, overlay textures)
6. Build Texture2DArray atlas (AtlasBuilder)
6.5. Patch texture indices in StateRegistry
7. Load BiomeDefinitions, OreDefinitions
8. Load ItemDefinitions
9. Load LootTables → convert to LootTableDefinition (Tier 2)
10. Load Tags → convert to TagDefinition, build TagRegistry
11. Load RecipeDefinitions → build CraftingEngine
12. Build ItemRegistry (block items + standalone items)
13. Load AssetBundle mods from `persistentDataPath/mods/*.lithmod`
14. BakeNative() → NativeStateRegistry + NativeAtlasLookup
15. Build ItemSpriteAtlas (item textures + block top faces → UI sprites)
16. Load SmeltingRecipeDefinitions → register BlockEntityFactories

**Mods**: Currently loaded as Unity AssetBundles (`.lithmod` files) containing ScriptableObjects. JSON-based content loading for mods is planned but NOT implemented.

## Job Data Flow

```
Main Thread ──schedule──► Worker Thread (Burst Job) ──produce──► NativeContainers
     ▲                                                                │
     └──────────────── poll JobHandle.IsCompleted ◄────────────────────┘
     Main thread: MegaMeshBuffer.Upload() → persistent GPU GraphicsBuffers
                                                    ↓
                                    FrustumCull.compute → per-chunk indirect args
                                                    ↓
                                    Graphics.RenderPrimitivesIndexedIndirect()
```

## ECS Scope

DOTS/ECS is **planned** for entity simulation (mobs, NPCs, projectiles) but NOT yet implemented.
No com.lithforge.entity package exists yet. Chunks, meshing, worldgen, lighting, crafting, content loading, and UI do not use ECS.

## Allocation Rules (Main Thread)

- **Fill pattern** for methods returning lists: caller passes a `List<T>`, callee calls `Clear()` then `Add()`. Never return `new List<T>()` from per-frame methods.
- **Cache reusable collections** as private fields (`_cache = new List<T>()`) and clear/reuse each frame. This applies to `LootResolver._dropCache`, `CraftingEngine._shapelessCache`, `ChunkManager._meshCandidateCache`, etc.
- **Cursor-based queue advancement**: instead of `list.RemoveRange(0, n)` (O(n) copy), track a `_queueIndex` cursor and reset when the queue drains.
- **Pre-parse hot-path values**: call `LootFunction.PreParseValues()` at load time so `ApplyFunctions()` reads cached ints instead of calling `int.TryParse()` per resolve.
- **Schwartzian transform** for sorting: pre-compute sort keys into a parallel array, then sort by key. Avoids repeated computation inside comparison delegates.

## Light System Invariants

- Light data is **nibble-packed**: high 4 bits = sunlight (0–15), low 4 bits = block light (0–15). Use `LightUtils.Pack/GetSunLight/GetBlockLight`.
- **Cross-chunk light propagation**: `LightPropagationJob` collects border light leaks into `NativeList<NativeBorderLightEntry>`. After job completion, `GenerationScheduler` copies these to managed `BorderLightEntry` on `ManagedChunk` and marks neighbors for `LightUpdateJob`.
- **Light removal on block edit**: `SetBlock()` sets chunk state to `RelightPending`. `RelightScheduler.ScheduleJobs()` runs `LightRemovalJob` (BFS removal + re-seed propagation) before meshing.
- **ChunkState FSM**: `Unloaded → Loading → Generating → Decorating → RelightPending → Generated → Meshing → Ready`. The `RelightPending` state gates meshing until relight completes. `Generated → Meshing` is handled by either MeshScheduler (LOD0) or LODScheduler (LOD>0).

## Storage Safety

- **Atomic file writes**: `RegionFile.Flush()` writes to a `.tmp` file, flushes, rotates the original to `.bak`, then renames `.tmp` to final. On failure, deletes `.tmp` and leaves the original untouched.
- **ChunkSerializer** uses `NativeArray.CopyTo(byte[])` and `CopyFrom(byte[])` for bulk light data transfer — never byte-by-byte loops.

## LOD System

Dual-path meshing from `Generated` state:

```
Generated ─┬─ LODLevel == 0 → MeshScheduler (GreedyMeshJob)                          → Ready
            └─ LODLevel > 0 → LODScheduler  (VoxelDownsampleJob + LODGreedyMeshJob) → Ready
```

- `LODScheduler.UpdateLODLevels()` assigns LOD levels to both Generated and Ready chunks based on Chebyshev XZ distance from camera chunk.
- LOD distances are configured in ChunkSettings: LOD1 (2x2x2 merge → 16³), LOD2 (4x4x4 → 8³), LOD3 (8x8x8 → 4³).
- When a Ready chunk's LOD level changes:
  - LOD0 → LOD>0: LODScheduler replaces the mesh with a downsampled version.
  - LOD>0 → LOD0: chunk state reset to `Generated`, MeshScheduler remeshes at full detail.
- MeshScheduler filters out LODLevel > 0 chunks (they belong to LODScheduler).
- Frustum culling on MeshScheduler **prioritizes** but does NOT block meshing — off-frustum chunks mesh if budget remains.
- LODGreedyMeshJob emits full-brightness vertices (AO=3, light=15) — no AO or lighting at LOD.
- VoxelDownsampleJob uses majority-vote: if >50% of source voxels are air, output is air; otherwise picks first opaque block.

**Invariant**: A chunk in `Generated` state MUST always have a path to `Ready`, regardless of its LODLevel.

## Known Limits

- **Texture array layers ≤ 1024**: `PackedMeshVertex.Word1` stores `texIndex` in 10 bits (0–1023) and `overlayTexIndex` in 10 bits (0–1023). ContentPipeline asserts at 1024.
- **BiomeData[i].BiomeId == i**: biome lookup is O(1) by direct index. This invariant is asserted at startup in `LithforgeBootstrap`. Both `SurfaceBuilderJob.GetBiome()` and `DecorationStage.GetBiome()` rely on this.
- **ChunkPool uses HashSet for checked-out tracking**: `Return()` is O(1) amortized, not O(n).

## Anti-Regression Rules

### Per-Frame Allocation Ban
Before committing, grep the diff for `new List`, `new Dictionary`, `new T[]` in methods
called each frame (Update, Poll, Process, Tick, Schedule). If found outside constructors
or `static readonly` fields, it's a bug. Convert to a private field with Clear()+reuse.

### Schedule+Complete Ban
Never call `job.Schedule().Complete()` in a loop. Batch-schedule all jobs, then call
`JobHandle.CompleteAll()` or `combinedHandle.Complete()` once.

### ChunkState Ordinal Invariant
`ChunkState` values are compared with `>=` and `<`. When adding a new state:
1. Decide where it falls in the lifecycle ordering
2. Grep ALL `ChunkState.` comparisons and verify correctness
3. Document the ordering invariant in the enum comment

### Burst Duplication Protocol
When duplicating code across IJob structs (unavoidable in Burst):
1. Add `// SHARED BFS LOGIC` header with list of all copies
2. Add entry in CLAUDE.md "Known Code Duplication" table
3. Any edit to one copy MUST be applied to all copies in the same commit

## Known Code Duplication (Burst constraint)

BFS light propagation methods are **unified** in `LightBfs` (`Packages/com.lithforge.worldgen/Runtime/Lighting/LightBfs.cs`).
All 3 light jobs (`LightPropagationJob`, `LightRemovalJob`, `LightUpdateJob`) call static methods on `LightBfs` — no duplication.

The following tint overlay packing logic is duplicated across 2 IJob structs.
**Any change to one MUST be replicated to the other in the same commit.**

| Method | GreedyMeshJob | LODGreedyMeshJob |
|--------|---------------|------------------|
| Tint overlay packing (baseTintType, overlayTintType, hasOverlay, overlayTexIdx bit packing) | ✓ | ✓ |

## Reference Sources

Local copies of reference implementations are available in `Sources/` (git-ignored):
- **Luanti** (`Sources/luanti-master/`): C++ voxel engine (Minetest fork) — ore types, biome selection, mapgen patterns
- **Minosoft** (`Sources/Minosoft-master/`): Kotlin Minecraft reimplementation — data-driven architecture, performance patterns

## Documentation Index

All documentation lives in `Docs/`:

| File | Content |
|------|---------|
| `Docs/01_PROJECT_OVERVIEW.md` | Vision, three-tier architecture, why Unity, targets |
| `Docs/02_SOLUTION_ARCHITECTURE.md` | Package structure, asmdef rules, dependency graph |
| `Docs/03_VOXEL_CORE.md` | Chunks (NativeArray), BlockState, StateRegistry, storage, invariants |
| `Docs/04_MESHING_AND_RENDERING.md` | Burst greedy meshing, GPU-driven rendering, PackedMeshVertex, compute culling |
| `Docs/05_WORLD_GENERATION.md` | Burst pipeline stages, NativeNoise, biomes, ores, cross-chunk decoration |
| `Docs/06_THREADING_AND_BURST.md` | Job scheduling, blittable types, NativeContainer ownership, memory budget |
| `Docs/07_DATA_DRIVEN_CONTENT.md` | File formats, loading pipeline, mod integration, validation |
| `Docs/08_REFERENCE_ANALYSIS.md` | Luanti & Minosoft comparison |
| `Docs/09_ROADMAP.md` | 5 sprints + post-MVP milestones |
| `Docs/10_ERROR_HANDLING.md` | Error categories, fallbacks, save recovery |
| `Docs/11_PLATFORM_ARCHITECTURE.md` | Game/mod/content pack hierarchy, version compat |
| `Docs/12_OBSERVABILITY.md` | Metrics, ProfilerMarker, logging, benchmarks |
| `Docs/13_CONVENTIONS.md` | Naming conventions: C#, Unity, DOTS/ECS, assets, shaders, namespaces |
| `Docs/14_MULTIPLAYER.md` | Multiplayer architecture: command layer, service extraction, physics, chunk protocol, inventory sync |
| `Docs/adr/` | Architecture Decision Records (ADR-001 through ADR-004) |