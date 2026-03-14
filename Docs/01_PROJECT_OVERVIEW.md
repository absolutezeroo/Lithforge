# Lithforge — Project Overview

## What Is Lithforge

Lithforge is an open-source voxel game-creation platform built with **Unity / C#**, designed as a modern, from-scratch reimplementation inspired by Luanti (formerly Minetest) and informed by Minecraft's data architecture. It is not a clone of either — it is a new engine that takes the best architectural decisions from both ecosystems and combines them with Unity's high-performance stack: **Burst**, **Jobs**, **NativeContainers**, and optionally **DOTS/ECS** where it provides measurable benefit.

## Vision Statement

A high-performance, fully data-driven, multi-threaded voxel engine where:

- The **domain logic** (registries, definitions, content loading, serialization, modding) is pure C# with zero engine dependency.
- **Performance-critical computation** (meshing, world generation, lighting, chunk storage) uses Unity's Burst compiler, Job System, and NativeContainers for maximum throughput.
- **Unity** serves as the rendering backend, UI framework, input system, and audio system via a thin runtime integration layer.
- Every game element — blocks, items, biomes, recipes, loot tables, world generation — is defined by **data files**, never hardcoded.
- The **modding API** is a first-class contract, not an afterthought.

## Three-Tier Architecture

The single most important architectural decision: Lithforge uses a **three-tier** dependency model.

```
┌─────────────────────────────────────────────────────────┐
│  TIER 1 — Pure C#                                       │
│  Zero Unity dependency. Compiles against .NET Standard.  │
│  Contains: data definitions, registries, content loading,│
│  serialization, mod manifest parsing, recipe matching.   │
│  Can be unit-tested without Unity.                       │
└──────────────────────────┬──────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────┐
│  TIER 2 — Unity Core                                    │
│  Depends on: Unity.Collections, Unity.Mathematics,       │
│  Unity.Jobs, Unity.Burst.                                │
│  Contains: chunk data (NativeArray), meshing jobs,       │
│  worldgen jobs, light propagation, voxel physics,        │
│  entity simulation (DOTS).                               │
│  Burst-compiled hot paths. No MonoBehaviours.            │
└──────────────────────────┬──────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────┐
│  TIER 3 — Unity Runtime                                  │
│  Depends on: UnityEngine, URP, UI Toolkit, InputSystem.  │
│  Contains: MonoBehaviours, mesh upload to GPU, material  │
│  management, UI screens, input handling, audio, camera,  │
│  scene management, debug overlays.                       │
└─────────────────────────────────────────────────────────┘
```

**Rule**: Tier 1 never references any Unity namespace. Tier 2 never references `UnityEngine` (only `Unity.Collections`, `Unity.Mathematics`, `Unity.Jobs`, `Unity.Burst`). Tier 3 consumes outputs from Tier 1 and Tier 2.

## Why Unity

| Criterion | Godot 4 | Unity | Decision Factor |
|-----------|---------|-------|-----------------|
| Burst compiler | Not available | SIMD-optimized native code from C# | Critical for meshing/worldgen perf |
| Job System | Manual ThreadPool | Structured, dependency-aware, Burst-integrated | Eliminates custom threading code |
| NativeContainers | Not available | Cache-friendly, Burst-compatible, explicit lifetime | Required for data-oriented hot paths |
| ECS (opt-in) | Not available | DOTS Entities for entity simulation | Batch processing of mobs/projectiles |
| Render pipeline | Godot renderer | URP with custom HLSL shaders | More control over voxel rendering |
| Mesh API | ArrayMesh (limited) | GraphicsBuffer + indirect draw | GPU-driven rendering, no per-chunk GameObjects |
| Ecosystem | Growing | Massive, proven in production | Tooling, profiling, platform support |
| Platform targets | Desktop + mobile | All platforms including consoles | Wider reach |

## What We Preserve From Luanti

- Mapgen diversity (v7-style 3D terrain, valleys, carpathian mountains) as configurable pipeline stages
- Ore generation types (scatter, sheet, puff, blob, vein, stratum)
- Schematic system for structure templates
- Temperature/humidity biome selection
- Content browser concept

## What We Preserve From Minecraft

- One file per definition (block, item, biome, recipe, loot table, model)
- BlockState property system with pre-computed state registry
- Model inheritance with texture variable resolution
- Multipart models for composable blocks (fences, walls)
- Tag system for grouping
- Namespace separation for mod isolation

## Non-Negotiable Constraints

1. **Three-tier separation enforced** — Tier 1 code never imports Unity namespaces. Tier 2 never imports UnityEngine.
2. **Burst-first for hot paths** — Meshing, worldgen, lighting, chunk access are Burst-compiled. No managed allocations in these paths.
3. **Explicit types everywhere** — No `var`. Allman braces. One file per type.
4. **Assembly definitions enforce boundaries** — Each package has an `.asmdef` with declared dependencies.
5. **Data-driven everything** — If it defines game content, it lives in a ScriptableObject loaded via `Resources.LoadAll<T>()`.
6. **Structured content directory** — Content lives in `Assets/Resources/Content/` organized by type (Blocks/, Items/, Biomes/, etc.).
7. **NativeContainer ownership is explicit** — Every NativeArray/NativeList has a documented owner and dispose point.

## Target Performance

| Metric | Target | Strategy |
|--------|--------|----------|
| Chunk generation (32³) | < 1ms | Burst-compiled noise + block placement |
| Greedy mesh build (32³) | < 0.5ms | Burst binary greedy, NativeArray output |
| Chunk GPU upload | < 0.3ms | MegaMeshBuffer persistent GraphicsBuffers |
| Light propagation (chunk) | < 0.3ms | Burst BFS with NativeQueue |
| 16-chunk render distance | > 60 FPS | LOD, frustum cull, occlusion |
| Memory per chunk | ~128 KB | Palette compression, NativeArray pooling |
| Cold start to playable | < 3s | Async content loading, progressive gen |

## License

LGPL-2.1-or-later (matching Luanti, allowing proprietary games/mods to link against the engine).
