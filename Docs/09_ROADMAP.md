# Lithforge — Implementation Roadmap (Unity)

## Sprint 0 — Foundation (Weeks 1-2) ✅ COMPLETED

**Goal**: Unity project scaffold, package structure, core types, Burst proof-of-concept.

### Deliverables

- [x] Unity project with URP configured
- [x] All local packages created (`com.lithforge.*`) with correct `.asmdef` and `package.json`
- [x] `.editorconfig` with enforced rules
- [x] `Lithforge.Core`: ResourceId, RegistryBuilder/Registry, ILogger, NullLogger, ContentValidator, ValidationResult
- [x] `Lithforge.Voxel`: ChunkConstants, StateId, BlockStateCompact, StateRegistry, NativeStateRegistry, ChunkData (NativeArray), ChunkPool
- [x] Content loading via ScriptableObjects in `Assets/Resources/Content/` + `Resources.LoadAll<T>()` (originally designed as JSON, migrated to SOs for editor ergonomics)
- [x] Native bake: `StateRegistry.BakeNative()` → `NativeStateRegistry`
- [x] Sample content: stone, dirt, grass_block, oak_log, water, air (ScriptableObject assets in Resources/Content/)
- [x] **Burst PoC**: one `[BurstCompile] IJob` that fills a `NativeArray<StateId>` with stone/air based on height → proves Burst pipeline works
- [x] `Lithforge.Core.Tests`: Registry, ResourceId, ContentValidator
- [x] `Lithforge.Voxel.Tests`: BlockDefinition loading, StateRegistry cartesian product, NativeStateRegistry bake, StateId round-trip
- [x] `docs/CLAUDE.md`, ADR-001 through ADR-003

### Acceptance Criteria

```
✓ Unity project opens and compiles with zero errors/warnings
✓ Assembly definition dependencies match the documented tier rules
✓ Lithforge.Core.asmdef does NOT reference any Unity assembly
✓ StateRegistry produces 3 states for oak_log (axis=x,y,z)
✓ StateRegistry produces 8 states for furnace (4 facing × 2 lit)
✓ NativeStateRegistry bake matches managed registry 1:1
✓ Burst PoC job runs and produces expected chunk data
✓ All tests pass in Unity Test Runner
✓ No Allocator.Persistent leaks (Unity safety checks enabled)
```

---

## Sprint 1 — See The World (Weeks 3-4) ✅ COMPLETED

**Goal**: Infinite terrain rendered in Unity with basic face-culled meshing.

### Deliverables

- [x] `Lithforge.WorldGen`: GenerationPipeline (managed orchestrator), TerrainShapeJob (Burst), NativeNoise, NativeNoiseConfig
- [x] `Lithforge.WorldGen`: SurfaceBuilderJob (Burst) — grass/dirt/stone layering
- [x] `Lithforge.Meshing`: MeshVertex (blittable, matching VertexAttributeDescriptor), MeshData (NativeList-based)
- [x] `Lithforge.Meshing`: Simple CulledMeshJob (Burst) — face culling only, no greedy yet
- [x] `Lithforge.Runtime.Bootstrap`: LithforgeBootstrap, InitializationSequence, ServiceContainer
- [x] `Lithforge.Runtime.Rendering`: ChunkRenderer (MeshFilter+MeshRenderer), MeshUploader (Mesh.MeshDataArray), VoxelMaterialManager
- [x] `Lithforge.Runtime.Rendering`: LithforgeVoxelOpaque.shader (basic URP, atlas UV, unlit initially)
- [x] `Lithforge.Runtime.Input`: Basic FPS camera (CharacterController + mouse look via InputSystem)
- [x] `Lithforge.Voxel`: ChunkManager (load/unload ring, spiral-out loading)
- [x] TextureAtlasManager: build Texture2DArray from content PNGs
- [x] GameLoop MonoBehaviour orchestrating Update() frame flow
- [x] Job dependency chain: generate → mesh → upload
- [x] DebugOverlayHUD: FPS, loaded chunks, pending jobs

### Acceptance Criteria

```
✓ Press Play → infinite terrain visible, extends in all directions
✓ WASD + mouse look camera movement
✓ Chunks load/unload as camera moves (visible in debug overlay)
✓ Terrain has height variation from noise
✓ Correct surface blocks: grass top, dirt below, stone deep
✓ No crashes after 5 minutes of free camera exploration
✓ Burst jobs visible in Unity Profiler timeline
✓ No native memory leaks (ChunkPool disposes correctly)
```

---

## Sprint 2 — Greedy & Beautiful (Weeks 5-6) ✅ COMPLETED

**Goal**: Greedy meshing, AO, lighting, proper texture atlas, benchmark comparison.

### Deliverables

- [x] `Lithforge.Meshing`: GreedyMeshJob (Burst binary greedy), AmbientOcclusion (Burst-compatible)
- [x] `Lithforge.Meshing`: AtlasBuilder, NativeAtlasLookup bake
- [x] `Lithforge.WorldGen`: InitialLightingJob (Burst sunlight), LightData (NativeArray<byte>) — lighting jobs live in worldgen package
- [x] Content asset loading: BlockStateMapping SO → BlockModel SO → parent resolution → per-face texture index
- [x] LithforgeVoxelOpaque.shader: AO, block light, sun light, Texture2DArray sampling
- [x] Benchmark: GreedyMeshJob vs CulledMeshJob (triangle count, job time)
- [x] `Lithforge.Voxel`: ChunkNeighborData extraction for cross-border meshing
- [x] Content: add oak_planks, cobblestone, sand, glass, torch models

### Acceptance Criteria

```
✓ Greedy meshing reduces triangle count >5x vs culled meshing (measured)
✓ AO darkening visible at block edges and corners
✓ Sunlight propagates correctly (dark under overhangs)
✓ Grass block has correct per-face textures (top/side/bottom)
✓ Oak log rotates correctly based on axis property
✓ GreedyMeshJob < 0.5ms per chunk (Burst, measured in Profiler)
✓ All textures resolved from content JSON files, no hardcoded paths
✓ BenchmarkDotNet results committed to repo
```

---

## Sprint 3 — Living World (Weeks 7-8) ✅ COMPLETED

**Goal**: Biomes, caves, ores, trees, world save/load.

### Deliverables

- [x] `Lithforge.WorldGen`: ClimateNoiseJob (Burst), SurfaceBuilderJob extended for biomes (biome assignment integrated into TerrainShapeJob)
- [x] `Lithforge.WorldGen`: CaveCarverJob (Burst — worm caves)
- [x] `Lithforge.WorldGen`: OreGenerationJob (Burst — scatter + blob)
- [x] `Lithforge.WorldGen`: DecorationStage (managed — oak tree templates)
- [x] Biome content: plains, forest, desert, mountains (ScriptableObjects with TreeTemplateIndex)
- [x] Ore content: coal, iron, gold, diamond (ScriptableObjects — blob + scatter types)
- [x] `Lithforge.Voxel.Storage`: RegionFile, ChunkSerializer (zstd), WorldMetadata
- [x] Save on quit, load on launch
- [x] `Lithforge.WorldGen`: LightPropagationJob (Burst BFS), LightRemovalJob, LightUpdateJob — all in worldgen/Runtime/Stages/
- [x] Day/night cycle: sun angle uniform driving shader sun_light_factor

### Acceptance Criteria

```
✓ Visible biome transitions (green→sand→snow)
✓ Caves accessible underground (worm caves)
✓ Ores visible in stone
✓ Trees on surface in forest/plains
✓ World persists across Play mode restarts
✓ Lighting updates when blocks removed underground
✓ Day/night cycle with smooth light transition
✓ Save/load round-trip: regenerated world matches original (deterministic)
```

---

## Sprint 4 — Interaction (Weeks 9-10) ✅ COMPLETED

**Goal**: Place/break blocks, inventory, crafting, HUD.

### Deliverables

- [x] `Lithforge.Physics`: VoxelRaycast (DDA, Burst-compatible), VoxelCollider (AABB vs grid)
- [x] `Lithforge.Runtime.Input`: BlockInteraction (place/break), PlayerController with gravity + voxel collision
- [x] `Lithforge.Voxel`: CraftingEngine, Inventory, RecipeEntry — crafting lives in voxel package (no separate crafting package)
- [x] `Lithforge.Voxel`: Loot table loading + drop resolution
- [x] `Lithforge.Voxel`: Tag loading + tool requirement checking
- [x] `Lithforge.Runtime.UI`: CrosshairHUD, HotbarDisplay, PlayerInventoryScreen (ContainerScreen base, UI Toolkit)
- [x] Block highlight wireframe (LineRenderer on targeted block)
- [x] Dirty chunk propagation on block change + remesh
- [x] Content: planks, sticks, wooden pickaxe recipes

### Acceptance Criteria

```
✓ Left click breaks targeted block (correct drops from loot table)
✓ Right click places block from hotbar
✓ Tab opens inventory with crafting grid
✓ Craft 4 logs → 16 planks works
✓ Stone requires pickaxe (tag-based check)
✓ Player has gravity and collides with blocks
✓ Hotbar shows 9 slots, selectable with 1-9
✓ Block change triggers immediate remesh of affected chunk(s)
```

---

## Sprint 5 — LOD & Performance (Weeks 11-12) ✅ COMPLETED

**Goal**: LOD system, culling, water, sky, performance pass.

### Deliverables

- [x] `Lithforge.Meshing`: VoxelDownsampleJob + LODGreedyMeshJob (Burst), LODMeshData
- [x] LOD levels 0-3 (full → half → quarter → eighth) via dual-path scheduling (MeshScheduler + LODScheduler)
- [x] Culling: frustum-prioritized meshing, forward-weighted generation queue, distance-based unload with save-on-unload
- [x] 3-submesh pipeline: opaque(0) → cutout(1) → translucent(2)
- [x] LithforgeVoxelCutout.shader (alpha test, Cull Off, double-sided)
- [x] BlockStateCompact.FlagFluid for water top-face height offset
- [x] LithforgeSky.shader (procedural skybox: sun disc, horizon-zenith gradient, star field)
- [x] SkyController + TimeOfDayController driving skybox, fog, ambient
- [x] SettingsScreen (UI Toolkit): render distance, FOV, AO, sensitivity, day length, volume
- [x] DebugOverlayHUD: renderer count, LOD queue count

### Acceptance Criteria

```
✓ 16-chunk render distance at >60 FPS (mid-range GPU)
✓ 24-chunk render distance at >30 FPS
✓ LOD transitions smooth (hysteresis, no visible pop)
✓ Memory < 1 GB native at 16-chunk distance
✓ Water visible and translucent
✓ Sky color changes with day/night
✓ Settings adjustable at runtime
✓ No Allocator.Persistent leaks across 10-minute play session
```

---

## Post-Sprint 5 — Completed Work

The following features have been implemented after Sprint 5:

### GPU-Driven Rendering Pipeline
- `ChunkMeshStore` + `MegaMeshBuffer`: persistent GPU vertex/index buffers per render layer (opaque, cutout, translucent)
- `Graphics.RenderPrimitivesIndexedIndirect` — zero GameObjects/MeshFilter/MeshRenderer per chunk
- `FrustumCull.compute` (3 kernels: CSResetCull, CSFrustumCull, CSOcclusionCull)
- `HiZPyramid` — Hi-Z occlusion culling from previous frame's depth buffer
- `PackedMeshVertex` — 16-byte bit-packed vertex (replaced 48-byte MeshVertex)

### Container UI Framework
- `ContainerScreen` (abstract MonoBehaviour base), `PlayerInventoryScreen`, `ChestScreen`, `FurnaceScreen`
- `SlotInteractionController` — Minecraft-style slot interaction (left/right click, shift-click, drag, number-key swap)
- `ContainerLayoutSO` + `SlotGroupDefinition` — data-driven UI layouts
- `ItemSpriteAtlas` + `ItemSpriteAtlasBuilder` — sprite generation for UI

### Biome Tinting
- `BiomeTintManager` — global biome parameter map (RGBA32, toroidal wrapping)
- Per-face tint types (grass/foliage/water colormaps)
- Dual-texture overlay system (base + overlay per face, alpha-blended in shader)

### Block Entity System
- `BlockEntityRegistry`, `BlockEntityTickScheduler`
- `ChestBlockEntity` + `ChestScreen` (container storage)
- `FurnaceBlockEntity` + `FurnaceScreen` (smelting with `SmeltingRecipeRegistry`)

### Content Additions
- `AffixDefinition` — data-driven mining modifiers via `IMiningModifier`
- `EnchantmentDefinition` — multi-level enchantment data
- `ToolSpeedProfile` — per-material mining speed overrides
- `BlockBehavior` + `BehaviorAction` subclasses

### Debug Infrastructure
- `FrameProfiler` — 14-section, zero-alloc, 300-frame rolling history
- `PipelineStats` — per-frame + cumulative counters
- `BenchmarkRunner` — F5-triggered automated benchmark with CSV output

---

## Post-MVP Milestones

### V1.0 — Playable Game (Sprints 6-10)

- Custom model meshing (stairs, slabs, fences)
- Multipart block resolution
- DOTS entity system (mobs with AI, health, movement)
- Sound system (block sounds, ambient per biome)
- Fluid simulation (water flow)
- More biomes, structures, ore types
- World creation screen, pause menu
- Content validation report UI

### V1.5 — Moddable Platform (Sprints 11-15)

- Data-only mod loading pipeline
- Mod manifest validation
- Content browser (discover + install)
- Hot-reload for data files (textures, models, definitions)
- Game manifest system (game.json)
- Modding documentation
- Example game + example mods

### V2.0 — Multiplayer (Sprints 16-20)

- Client-server networking
- Server-authoritative world state
- Player synchronization
- Chat system
- Dedicated server (headless Unity build)
- C# code modding (Mono runtime only)

---

## Technical Debt Prevention

1. **No TODO without issue**: Every `// TODO` references a tracked issue.
2. **Tests for every domain**: Public APIs in Tier 1/2 packages have unit tests.
3. **Benchmarks checked in**: GreedyMeshJob, TerrainShapeJob, NoiseSampling benchmarks in CI.
4. **Tier boundary enforcement**: CI validates that `.asmdef` references match documented tier rules.
5. **Safety checks ON in editor**: NativeContainer leak detection, job safety checks enabled.
6. **Safety checks OFF in builds**: `ENABLE_UNITY_COLLECTIONS_CHECKS` stripped for performance.
7. **Content validation**: ScriptableObject references validated by ContentPipeline at startup (missing refs logged as warnings).
8. **Memory budget tracking**: Debug overlay tracks native memory. Alert if budget exceeds target.
9. **ProfilerMarker on every hot path**: Every Burst job and every main-thread operation has a marker.
10. **ADR for every architectural decision**: New ADR required before any structural change.
