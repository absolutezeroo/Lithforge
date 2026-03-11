# Lithforge — CLAUDE.md (AI Development Context)

## Project Identity

- **Name**: Lithforge
- **Type**: Open-source voxel game-creation platform
- **Engine**: Unity / C# with Burst, Jobs, NativeContainers, DOTS (entities only)
- **Render Pipeline**: URP (Universal Render Pipeline)
- **Architecture**: Three-tier (Pure C# → Unity Core → Unity Runtime)
- **Data convention**: Minecraft-style (assets/data split, one file per definition)
- **License**: LGPL-2.1-or-later

## Three-Tier Architecture

```
Tier 1 — Pure C# (no Unity dependency)
  Packages: Core, Crafting, Modding
  Contains: definitions, registries, content loading, serialization, validation

Tier 2 — Unity Core (Unity.Collections, Unity.Mathematics, Unity.Burst, Unity.Jobs)
  Packages: Voxel, WorldGen, Meshing, Lighting, Physics, Entity
  Contains: NativeArray chunk data, Burst-compiled jobs, NativeStateRegistry
  NO UnityEngine references.

Tier 3 — Unity Runtime (UnityEngine, URP, InputSystem, UI Toolkit)
  Location: Assets/Lithforge.Runtime/
  Contains: MonoBehaviours, mesh upload, shaders, UI, input, audio, debug
```

## Critical Rules

### Code Conventions (All Tiers)

1. **No `var`** — explicit types everywhere, enforced by `.editorconfig` as error.
2. **Allman braces** — opening brace on new line, always.
3. **One file per type** — each class, interface, enum, struct gets its own file.
4. **Namespace = package path** — `namespace Lithforge.Voxel.Block;`
5. **Private fields: `_camelCase`** — underscore prefix.
6. **No expression-bodied methods** — block bodies only.
7. **Interfaces: `I` prefix** — `IRegistry`, `ILogger`.
8. **Accessibility always explicit** — `public`, `private`, `internal`.

### Tier 1 Rules

9. **Zero Unity references** — `Lithforge.Core.asmdef` must not reference any Unity assembly.
10. **Standard .NET types only** — `Dictionary`, `List`, `string`, etc.
11. **Unit-testable in isolation** — could compile against .NET Standard.

### Tier 2 Rules (Burst/Jobs)

12. **`[BurstCompile]` on all hot-path job structs.**
13. **`Unity.Mathematics` types** — `float3`, `int3`, `math.sin()` not `System.Math`.
14. **`NativeArray<T>`** not managed arrays for chunk data, mesh data, light data.
15. **No heap allocation in Burst paths** — no `new`, no boxing, no string ops.
16. **No try/catch in Burst paths.**
17. **No interface dispatch in Burst paths** — no virtual calls.
18. **No `class` references in Burst jobs** — only blittable structs.
19. **`[ReadOnly]` attribute on job inputs** that are not modified.
20. **`Allocator.TempJob`** for per-frame data, **`Allocator.Persistent`** for long-lived data.
21. **Every NativeContainer has a documented owner and dispose point.**
22. **ChunkPool** for NativeArray recycling — no per-chunk Persistent allocation.

### Tier 3 Rules

23. **Mesh upload on main thread only** — `Mesh.ApplyAndDisposeWritableMeshData`.
24. **No Burst code** in Tier 3 — Tier 3 consumes job results, does not produce them.
25. **Shaders are hand-written HLSL** (URP-compatible), not Shader Graph.

## Key Types

| Type | Tier | Blittable | Purpose |
|------|------|-----------|---------|
| `ResourceId` | 1 | No (string) | "namespace:name" identifier |
| `Registry<T>` | 1 | No (managed) | Frozen read-only registry |
| `BlockDefinition` | 1 | No (managed) | Data-driven block properties |
| `StateId` | 2 | Yes (ushort) | Index into NativeStateRegistry |
| `BlockStateCompact` | 2 | Yes (struct) | Cached render/physics flags for Burst |
| `NativeStateRegistry` | 2 | Yes (NativeArray) | Burst-accessible state lookup |
| `ChunkData` | 2 | Yes (NativeArray) | 32³ voxel storage |
| `MeshVertex` | 2 | Yes (struct) | 40-byte vertex for GPU |
| `NativeNoiseConfig` | 2 | Yes (struct) | Noise parameters for Burst jobs |
| `NativeBiomeData` | 2 | Yes (struct) | Biome data for Burst jobs |

## Dual Representation Pattern

Types that need both managed (content loading, modding) and Burst-compatible forms:

```
BlockState (managed, full properties) ──BakeNative()──► BlockStateCompact (blittable)
Registry<BlockDefinition> ──BakeNative()──► NativeStateRegistry (NativeArray)
BiomeDefinition (managed) ──BakeNative()──► NativeBiomeData (blittable)
OreDefinition (managed) ──BakeNative()──► NativeOreConfig (blittable)
Atlas regions (managed) ──BakeNative()──► NativeAtlasLookup (NativeArray)
```

Baking happens once at content load freeze. Both representations must be kept in sync. The managed version is authoritative (source of truth); the native version is derived.

## Content Files

Location: `Assets/StreamingAssets/content/` (core) or `{persistentDataPath}/mods/` (mods)

```
Block definition:     data/{ns}/blocks/{id}.json
BlockState mapping:   assets/{ns}/blockstates/{id}.json
Block model:          assets/{ns}/models/block/{id}.json
Item model:           assets/{ns}/models/item/{id}.json
Loot table:           data/{ns}/loot_tables/blocks/{id}.json
Tags:                 data/{ns}/tags/blocks/{tag}.json
Recipes:              data/{ns}/recipes/{type}/{id}.json
Biomes:               data/{ns}/worldgen/biome/{id}.json
Noise settings:       data/{ns}/worldgen/noise_settings/{id}.json
```

## Job Data Flow

```
Main Thread ──schedule──► Worker Thread (Burst Job) ──produce──► NativeContainers
     ▲                                                                │
     └──────────────── poll JobHandle.IsCompleted ◄────────────────────┘
     Main thread: upload mesh to GPU, dispose temp NativeContainers
```

## ECS Scope

DOTS/ECS is used **ONLY** for entity simulation (mobs, NPCs, projectiles).
NOT for: chunks, meshing, worldgen, lighting, crafting, content loading, modding, UI.

## Allocation Rules (Main Thread)

- **Fill pattern** for methods returning lists: caller passes a `List<T>`, callee calls `Clear()` then `Add()`. Never return `new List<T>()` from per-frame methods.
- **Cache reusable collections** as private fields (`_cache = new List<T>()`) and clear/reuse each frame. This applies to `LootResolver._dropCache`, `CraftingEngine._shapelessCache`, `ChunkManager._meshCandidateCache`, etc.
- **Cursor-based queue advancement**: instead of `list.RemoveRange(0, n)` (O(n) copy), track a `_queueIndex` cursor and reset when the queue drains.
- **Pre-parse hot-path values**: call `LootFunction.PreParseValues()` at load time so `ApplyFunctions()` reads cached ints instead of calling `int.TryParse()` per resolve.
- **Schwartzian transform** for sorting: pre-compute sort keys into a parallel array, then sort by key. Avoids repeated computation inside comparison delegates.

## Light System Invariants

- Light data is **nibble-packed**: high 4 bits = sunlight (0–15), low 4 bits = block light (0–15). Use `LightUtils.Pack/GetSunLight/GetBlockLight`.
- **Cross-chunk light propagation**: `LightPropagationJob` collects border light leaks into `NativeList<NativeBorderLightEntry>`. After job completion, `GenerationScheduler` copies these to managed `BorderLightEntry` on `ManagedChunk` and marks neighbors for `LightUpdateJob`.
- **Light removal on block edit**: `SetBlock()` sets chunk state to `RelightPending`. `MeshScheduler.ProcessRelightPending()` runs `LightRemovalJob` (BFS removal + re-seed propagation) before meshing.
- **ChunkState FSM**: `Unloaded → Loading → Generating → Decorating → RelightPending → Generated → Meshing → Ready`. The `RelightPending` state gates meshing until relight completes.

## Storage Safety

- **Atomic file writes**: `RegionFile.Flush()` writes to a `.tmp` file, flushes, rotates the original to `.bak`, then renames `.tmp` to final. On failure, deletes `.tmp` and leaves the original untouched.
- **ChunkSerializer** uses `NativeArray.CopyTo(byte[])` and `CopyFrom(byte[])` for bulk light data transfer — never byte-by-byte loops.

## Known Limits

- **Texture array layers ≤ 1024**: `MeshVertex.Color.a` stores texture index as `half`. The `half` type represents integers exactly up to 2048, but ContentPipeline asserts at 1024 for safety margin.
- **BiomeData[i].BiomeId == i**: biome lookup is O(1) by direct index. This invariant is asserted at startup in `LithforgeBootstrap`. Both `SurfaceBuilderJob.GetBiome()` and `DecorationStage.GetBiome()` rely on this.
- **ChunkPool uses HashSet for checked-out tracking**: `Return()` is O(1) amortized, not O(n).

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
| `Docs/04_MESHING_AND_RENDERING.md` | Burst greedy meshing, URP shaders, Texture2DArray, MeshUploader |
| `Docs/05_WORLD_GENERATION.md` | Burst pipeline stages, NativeNoise, biomes, ores, cross-chunk decoration |
| `Docs/06_THREADING_AND_BURST.md` | Job scheduling, blittable types, NativeContainer ownership, memory budget |
| `Docs/07_DATA_DRIVEN_CONTENT.md` | File formats, loading pipeline, mod integration, validation |
| `Docs/08_REFERENCE_ANALYSIS.md` | Luanti & Minosoft comparison |
| `Docs/09_ROADMAP.md` | 5 sprints + post-MVP milestones |
| `Docs/10_ERROR_HANDLING.md` | Error categories, fallbacks, save recovery |
| `Docs/11_PLATFORM_ARCHITECTURE.md` | Game/mod/content pack hierarchy, version compat |
| `Docs/12_OBSERVABILITY.md` | Metrics, ProfilerMarker, logging, benchmarks |
| `Docs/adr/` | Architecture Decision Records |