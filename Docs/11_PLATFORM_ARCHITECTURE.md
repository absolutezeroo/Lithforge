# Lithforge — Platform Architecture

## Current Architecture

Lithforge is currently a **single-game voxel engine**. All content (blocks, items, biomes, recipes, etc.) is defined as Unity ScriptableObjects in `Assets/Resources/Content/`. There is no separate game/mod/content-pack layer yet.

### Content Hierarchy (Current)

```
┌─────────────────────────────────────────────────┐
│  Lithforge Engine                                │
│  Packages/com.lithforge.* (core, voxel,          │
│  worldgen, meshing, physics)                     │
│  Provides: runtime, rendering, physics, worldgen │
└──────────────────┬──────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────┐
│  Core Content (ScriptableObjects)                │
│  Assets/Resources/Content/                       │
│  Blocks, BlockStates, Models, Items, Biomes,     │
│  Ores, Recipes, LootTables, Tags, Textures       │
└──────────────────┬──────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────┐
│  Settings (ScriptableObjects)                    │
│  Assets/Resources/Settings/                      │
│  ChunkSettings, WorldGenSettings, Rendering,     │
│  Physics, Gameplay, Debug                        │
└─────────────────────────────────────────────────┘
```

### Bootstrap & Loading

**`LithforgeBootstrap.Awake()`**:
1. `SettingsLoader.Load()` — loads all 6 Settings SOs via `Resources.Load<T>("Settings/...")`
   with `LoadOrCreate<T>()` fallback (creates transient default if missing)

**`LithforgeBootstrap.Start()`** (coroutine):
2. Creates `LoadingScreen` early (UI Toolkit, sortingOrder=500)
3. Runs `ContentPipeline.Build()` as `IEnumerable<string>`, yielding a frame between each phase:
   - Phases 1-6: Blocks → StateRegistry → ModelResolver → AtlasBuilder → texture patch
   - Phase 7: BiomeDefinitions, OreDefinitions
   - Phase 8: ItemDefinitions
   - Phases 9-12: LootTables, Tags, Recipes, ItemRegistry
   - Phase 13: AssetBundle mods from `persistentDataPath/mods/*.lithmod`
   - Phase 14: BakeNative() → NativeStateRegistry + NativeAtlasLookup
4. Initialize ChunkPool, ChunkManager, GenerationPipeline, WorldStorage
5. Initialize GameLoop with GenerationScheduler, MeshScheduler, LODScheduler
6. Initialize SpawnManager, UI (HotbarHUD, CrosshairHUD, InventoryScreen, SettingsScreen), Input

### Content Definition Types

| ScriptableObject | Location | Purpose |
|---|---|---|
| BlockDefinition | Content/Blocks/ | Block properties (hardness, render layer, collision, light) |
| BlockStateMapping | Content/BlockStates/ | State→model mapping per property variant |
| BlockModel | Content/Models/ | Face textures, parent inheritance (cube, cube_all, cube_column) |
| ItemDefinition | Content/Items/ | Item properties (stack size, tool type, mining speed) |
| BiomeDefinition | Content/Biomes/ | Temperature/humidity ranges, surface blocks, tree template index |
| OreDefinition | Content/Ores/ | Vein size, frequency, Y range, ore type |
| RecipeDefinition | Content/Recipes/ | Shaped/shapeless crafting patterns |
| LootTable | Content/LootTables/ | Block drop rules with pools, entries, and functions |
| Tag | Content/Tags/ | Block groupings (mineable_pickaxe, logs, leaves, planks, etc.) |

### Dual Representation

Managed ScriptableObjects are baked to Burst-compatible native structs at startup:

```
BlockDefinition (SO) → StateRegistry ──BakeNative()──► NativeStateRegistry (NativeArray<BlockStateCompact>)
BiomeDefinition (SO) ──BakeNative()──► NativeBiomeData (blittable)
OreDefinition (SO)   ──BakeNative()──► NativeOreConfig (blittable)
AtlasRegions         ──BakeNative()──► NativeAtlasLookup (NativeArray)
```

Baking happens once during `ContentPipeline.Build()` Phase 14. The managed SO is authoritative; the native version is derived and read-only.

### World Storage

Worlds are saved in `Application.persistentDataPath/worlds/{worldName}/`:
- `world.json` — metadata: seed, version, content_hash, last_saved (written by `WorldMetadata.Save()`)
- `region/r.{rx}.{ry}.{rz}.lfrg` — region files (atomic write with .tmp + .bak rotation)
- Chunks marked `IsDirty` are saved on unload via `WorldStorage.SaveChunk()`

---

## Mod Support (AssetBundle-Based — Minimal)

The current mod system uses Unity **AssetBundles**:

- Mod files: `Application.persistentDataPath/mods/*.lithmod`
- `ModLoader.LoadAllMods()` scans for `.lithmod` files, loads them via `AssetBundle.LoadFromFile()`
- Each bundle contains ScriptableObject assets (same types as core content)
- Loaded mod blocks are registered into `StateRegistry` after core content (Phase 13)
- Other mod content types (items, biomes, etc.) are loaded but not yet fully wired into the pipeline

`ModManifest.cs` and `ModDependency.cs` exist in `Assets/Lithforge.Runtime/Content/Mods/` but are not yet used for dependency resolution or load ordering.

---

## Future: Full Modding Architecture (NOT YET IMPLEMENTED)

The following is the planned architecture for comprehensive mod support. **None of this exists in code yet.**

### Planned Features

- **JSON-based content files** alongside ScriptableObjects (SOs for core content, JSON for mods)
- **game.json manifests** defining complete games on top of the engine
- **mod.json manifests** with dependency declarations and version constraints
- **Content pack system** (texture packs, sound packs, language packs)
- **Filesystem-based content loading** via StreamingAssets + persistentDataPath
- **Topological sort** of mod load order with cycle detection
- **Tag merging** (additive or replace modes across namespaces)
- **Semver-based version compatibility** (engine→game→mod)

### Planned Content Hierarchy

```
Engine  →  Game (game.json)  →  Mods (mod.json)  →  Content Packs (pack.json)
```

### Planned Content Loading Order

```
1. Load engine core content
2. Detect and validate game manifest (game.json)
3. Load game content
4. Discover mods, validate dependencies, topological sort
5. Load mods in dependency order
6. Merge tags across namespaces
7. Build registries, freeze, bake native data
```

This architecture will enable third-party game creation and community modding on top of the Lithforge engine.
