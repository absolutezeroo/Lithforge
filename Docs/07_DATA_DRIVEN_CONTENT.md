# Lithforge — Data-Driven Content System

## Content Architecture

All game content is defined as **Unity ScriptableObjects** stored in `Assets/Resources/Content/` and loaded at startup via `Resources.LoadAll<T>()`. Settings are separate ScriptableObjects in `Assets/Resources/Settings/`.

This approach provides:
- Editor-friendly asset creation and editing via Unity Inspector
- Type-safe references between content assets (e.g., `BlockDefinition` references a `LootTable`)
- No filesystem parsing or JSON deserialization at runtime for core content
- Unity's asset pipeline handles serialization and build inclusion

---

## Content Directory Structure

```
Assets/Resources/
├── Content/
│   ├── Blocks/          19x BlockDefinition.asset
│   ├── BlockStates/     19x BlockStateMapping.asset
│   ├── Models/          16x BlockModel.asset (cube, cube_all, cube_column, furnace, etc.)
│   ├── Items/           21x ItemDefinition.asset
│   ├── ItemModels/      ~17x BlockModel.asset (reuses BlockModel format for items)
│   ├── LootTables/      ~21x LootTable.asset
│   ├── Tags/            6x Tag.asset
│   ├── Recipes/         13x RecipeDefinition.asset
│   ├── Biomes/          4x BiomeDefinition.asset
│   ├── Ores/            4x OreDefinition.asset
│   ├── Layouts/        ContainerLayoutSO.asset (data-driven UI layouts)
│   └── Textures/
│       ├── Blocks/      ~30x Texture2D PNG (block face textures, 16x16)
│       └── Items/       ~27x Texture2D PNG (item icons)
│
└── Settings/
    ├── ChunkSettings.asset       render distance, LOD distances, spawn radius, mesh budget
    ├── WorldGenSettings.asset    noise configs (terrain, cave, temperature, humidity), sea level, seed
    ├── RenderingSettings.asset   sky/fog/ambient gradients, day length, atlas tile size
    ├── PhysicsSettings.asset     player dimensions, gravity, jump force, step height
    ├── GameplaySettings.asset    starting items list
    └── DebugSettings.asset       debug overlay toggles
```

---

## ScriptableObject Definitions

### BlockDefinition

`Assets/Lithforge.Runtime/Content/Blocks/BlockDefinition.cs` — `[CreateAssetMenu]`

| Field | Type | Purpose |
|-------|------|---------|
| `_namespace` | string | Content namespace (e.g., "lithforge") |
| `blockName` | string | Block identifier (e.g., "oak_log") |
| `hardness` | double | Mining time multiplier |
| `blastResistance` | double | Explosion resistance |
| `requiresTool` | bool | Whether a tool is needed to get drops |
| `soundGroup` | string | Sound category |
| `collisionShape` | CollisionShapeType | FullCube, None, etc. |
| `renderLayer` | RenderLayerType | Opaque, Cutout, Translucent |
| `isFluid` | bool | Fluid behavior flag |
| `lightEmission` | int (0-15) | Light emitted |
| `lightFilter` | int (0-15) | Light absorption |
| `mapColor` | string | Minimap color |
| `lootTable` | LootTable (SO ref) | Drop rules |
| `blockStateMapping` | BlockStateMapping (SO ref) | State→model mapping |
| `properties` | List\<BlockPropertyEntry\> | State properties (axis, facing, lit, etc.) |
| `materialType` | BlockMaterialType | Physical material (determines mining speed per tool) |
| `requiredToolLevel` | int | Minimum tool level (0=none, 1=wood, 2=stone...) |
| `defaultTintType` | int (-1 to 3) | Biome tint applied when model has no per-face tintIndex |
| `tags` | List\<string\> | Tag membership |

`ComputeStateCount()` returns the cartesian product of all property value counts.

### BlockStateMapping

`Assets/Lithforge.Runtime/Content/Blocks/BlockStateMapping.cs`

Maps variant keys to `BlockModel` references:
- `variants`: List of `BlockStateVariantEntry`, each with `VariantKey` (e.g., `"facing=north,lit=false"`) and `Model` (BlockModel SO reference)

### BlockModel

`Assets/Lithforge.Runtime/Content/Models/BlockModel.cs`

| Field | Type | Purpose |
|-------|------|---------|
| `builtInParent` | BuiltInParentType | cube, cube_all, cube_column, or None |
| `parentModel` | BlockModel (SO ref) | Parent for inheritance chain |
| `elements` | List\<ModelElement\> | Geometry elements |
| `textures` | List\<TextureVariable\> | Texture variable bindings (key → Texture2D) |

Parent inheritance is resolved by `ContentModelResolver` during `ContentPipeline.Build()`. Built-in parents (`cube`, `cube_all`, `cube_column`) provide standard face layouts.

### BiomeDefinition

`Assets/Lithforge.Runtime/Content/WorldGen/BiomeDefinition.cs`

| Field | Type | Purpose |
|-------|------|---------|
| `temperatureMin/Max/Center` | float | Climate range for biome selection |
| `humidityMin/Max/Center` | float | Moisture range |
| `topBlock` | BlockDefinition (SO ref) | Surface block (grass, sand) |
| `fillerBlock` | BlockDefinition (SO ref) | Sub-surface (dirt, sandstone) |
| `stoneBlock` | BlockDefinition (SO ref) | Deep layer |
| `underwaterBlock` | BlockDefinition (SO ref) | Underwater floor |
| `fillerDepth` | int | Layers of filler block |
| `treeDensity` | float | Tree placement probability per column |
| `heightModifier` | float | Terrain height adjustment |
| `treeType` | int (0-2) | Tree template: 0=Oak, 1=Birch, 2=Spruce |

### OreDefinition

`Assets/Lithforge.Runtime/Content/WorldGen/OreDefinition.cs`

| Field | Type | Purpose |
|-------|------|---------|
| `oreBlock` | BlockDefinition (SO ref) | Block to place |
| `replaceBlock` | BlockDefinition (SO ref) | Block to replace (usually stone) |
| `minY / maxY` | int | Y range for generation |
| `veinSize` | int | Cluster size |
| `frequency` | float | Placement density |
| `oreType` | OreType | Blob or Scatter |

### ItemDefinition

`Assets/Lithforge.Runtime/Content/Items/ItemDefinition.cs`

| Field | Type | Purpose |
|-------|------|---------|
| `maxStackSize` | int (1-64) | Stack limit |
| `toolType` | ToolType | None, Pickaxe, Axe, Shovel, Sword, Hoe |
| `toolLevel` | int | Tool tier |
| `durability` | int | Uses before breaking |
| `attackDamage / attackSpeed` | float | Combat stats |
| `miningSpeed` | float | Tool speed multiplier |
| `placesBlock` | BlockDefinition (SO ref) | Block placed on right-click |
| `itemModel` | BlockModel (SO ref) | Display model |
| `toolSpeedProfile` | ToolSpeedProfile (SO ref) | Speed profile per block material (overrides miningSpeed) |
| `affixes` | AffixDefinition[] | Mining modifier affixes |
| `fuelTime` | float | Fuel burn time in seconds (0 = not fuel) |
| `tags` | List\<string\> | Tag membership |

### ToolSpeedProfile

`Assets/Lithforge.Runtime/Content/Tools/ToolSpeedProfile.cs` — `[CreateAssetMenu]`

Data-driven per-material mining speed overrides. When assigned to an `ItemDefinition`, it replaces the flat `miningSpeed` with per-`BlockMaterialType` speed values. Used for tool-tier differentiation (e.g., diamond pickaxe faster on stone than on dirt).

### AffixDefinition

`Assets/Lithforge.Runtime/Content/Items/Affixes/AffixDefinition.cs` — `[CreateAssetMenu]`

Data-driven mining modifier. Implements `IMiningModifier` (Tier 2 interface). Affixes are sorted by `Priority` at load time and applied in sequence to `MiningContext` during block break calculations. Each affix has an `AffixEffectType` and `AffixMiningEffect` that modify speed, damage, or other mining parameters.

### EnchantmentDefinition

`Assets/Lithforge.Runtime/Content/Items/Enchantments/EnchantmentDefinition.cs` — `[CreateAssetMenu]`

Multi-level enchantment data. Each enchantment has an `EnchantmentCategory` and a list of `EnchantmentLevelData` entries defining per-level stat modifiers. Planned for use in the enchanting system (NOT yet wired into gameplay).

### BlockBehavior

`Assets/Lithforge.Runtime/Content/Blocks/BlockBehavior.cs`

Defines a list of `BehaviorAction` subclasses triggered on block events. Available action types:
- `GiveItemAction` — adds item to player inventory
- `PlaySoundAction` — plays a sound effect
- `SetBlockAction` — changes a block in the world
- `SpawnEntityAction` — spawns an entity at block position
- `SpawnParticleAction` — emits particles

### ContainerLayoutSO

`Assets/Lithforge.Runtime/UI/Layout/ContainerLayoutSO.cs` — `[CreateAssetMenu]`

Data-driven UI layout for container screens (chest, furnace, etc.). Contains a list of `SlotGroupDefinition` entries, each defining a grid region (rows, columns, position offset) in the container UI. Used by `ContainerScreen` subclasses to generate slot grids from data rather than code.

### LootTable

`Assets/Lithforge.Runtime/Content/Loot/LootTable.cs`

Nested structure: `LootTable` → `List<LootPoolEntry>` → `List<LootItemEntry>` → `List<LootFunctionEntry>`

Each `LootFunctionEntry` has a `functionType` and key-value parameters (`List<StringPair>`). Functions are pre-parsed at load time via `PreParseValues()`.

### Tag

`Assets/Lithforge.Runtime/Content/Tags/Tag.cs`

| Field | Type | Purpose |
|-------|------|---------|
| `replace` | bool | True = override, False = additive merge |
| `entryIds` | List\<string\> | Block/item IDs in this tag |

### RecipeDefinition

`Assets/Lithforge.Runtime/Content/Recipes/RecipeDefinition.cs`

| Field | Type | Purpose |
|-------|------|---------|
| `type` | RecipeType | Shaped or Shapeless |
| `resultItem` | ItemDefinition (SO ref) | Output item |
| `resultCount` | int | Output stack size |
| `pattern` | List\<string\> | Shaped grid pattern |
| `keys` | List\<RecipeKeyEntry\> | Pattern character → item mapping |
| `ingredients` | List\<RecipeIngredient\> | Shapeless ingredient list |

---

## Content Loading Pipeline

`ContentPipeline.Build()` is an `IEnumerable<string>` that yields phase description strings. `LithforgeBootstrap.Start()` iterates it as a coroutine, yielding a frame between phases so the `LoadingScreen` can update.

### Phase Sequence (16 phases)

| # | Phase | What Happens |
|---|-------|-------------|
| 1 | Load Blocks | `Resources.LoadAll<BlockDefinition>("Content/Blocks")` → 19 SOs |
| 2 | Build StateRegistry | Iterate blocks, compute cartesian product of properties, create `BlockRegistrationData` per block |
| 2.5 | Load Block Entities | `Resources.LoadAll<BlockEntityDefinitionSO>("Content/BlockEntities")` → patch `StateRegistry` with block entity type IDs |
| 3 | Resolve Models | Create `ContentModelResolver`, walk `BlockModel` parent inheritance chains |
| 4 | Resolve Textures | For every state, match `BlockStateMapping.Variants` by `BuildVariantKey()`, resolve per-face `Texture2D` references |
| 5 | Resolve Overlays & Tints | Resolve per-face tint types and overlay textures from model elements (must run before atlas build) |
| 6 | Build Atlas | `AtlasBuilder.Build()` → pack all `Texture2D` objects (base + overlay) into a `Texture2DArray` |
| 6.5 | Patch Indices | Iterate resolved faces, call `stateRegistry.PatchTextures()` to embed atlas layer indices |
| 7 | Load WorldGen | `Resources.LoadAll<BiomeDefinition>("Content/Biomes")` + `Resources.LoadAll<OreDefinition>("Content/Ores")` |
| 8 | Load Items | `Resources.LoadAll<ItemDefinition>("Content/Items")` |
| 9 | Load Loot | `Resources.LoadAll<LootTable>("Content/LootTables")` → convert to `LootTableDefinition` (Tier 2), call `PreParseValues()` |
| 10 | Load Tags | `Resources.LoadAll<Tag>("Content/Tags")` → convert to `TagDefinition`, build `TagRegistry` |
| 11 | Load Recipes | `Resources.LoadAll<RecipeDefinition>("Content/Recipes")` → convert to `RecipeEntry`, build `CraftingEngine` |
| 12 | Build ItemRegistry | Convert `ItemDefinition` SOs to `ItemEntry` objects, register block items + standalone items |
| 13 | Load Mods | `ModLoader.LoadAllMods()` — scan `persistentDataPath/mods/*.lithmod` AssetBundles, load their SOs, register mod blocks |
| 14 | Bake Native | `stateRegistry.BakeNative(Persistent)` → `NativeStateRegistry`; `BakeAtlasLookup()` → `NativeAtlasLookup` (includes overlay texture indices and per-face tint packing) |
| 15 | Build Item Sprites | `ItemSpriteAtlasBuilder.Build()` → generate `ItemSpriteAtlas` (Dictionary<ResourceId, Sprite>) for UI display |
| 16 | Load Smelting & Block Entity Factories | `Resources.LoadAll<SmeltingRecipeDefinition>("Content/Recipes/Smelting")` → build `SmeltingRecipeRegistry`; register `BlockEntityRegistry` (ChestBlockEntityFactory, FurnaceBlockEntityFactory) |

### ContentPipelineResult

The pipeline produces a `ContentPipelineResult` containing:
- `StateRegistry` (managed, Tier 2)
- `NativeStateRegistry` (`NativeArray<BlockStateCompact>`, Tier 2 — Burst-accessible)
- `NativeAtlasLookup` (`NativeArray<AtlasEntry>`, Tier 2 — Burst-accessible, includes overlay indices + per-face tint)
- `AtlasResult` (`Texture2DArray` + index map, Tier 3)
- `BiomeDefinitions[]`, `OreDefinitions[]` (SO references)
- `ItemEntries`, `LootTables`, `TagRegistry`, `ItemRegistry`, `CraftingEngine` (managed)
- `ItemSpriteAtlas` (Dictionary<ResourceId, Sprite> for UI)
- `BlockEntityRegistry` (block entity type factories)
- `SmeltingRecipeRegistry` (smelting recipes)

This result is passed to `GameLoop`, `GenerationPipeline`, and other systems.

---

## Dual Representation Pattern

ScriptableObjects are the authoritative source of truth. At startup, they are baked to Burst-compatible blittable structs:

```
BlockDefinition (SO) → StateRegistry ──BakeNative()──► NativeStateRegistry (NativeArray<BlockStateCompact>)
BiomeDefinition (SO) ──BakeNative()──► NativeBiomeData (blittable struct)
OreDefinition (SO)   ──BakeNative()──► NativeOreConfig (blittable struct)
Atlas face textures  ──BakeNative()──► NativeAtlasLookup (NativeArray)
```

Baking happens once during Phase 14 of `ContentPipeline.Build()`. After baking:
- The `NativeStateRegistry` is passed as `[ReadOnly]` input to all Burst jobs.
- The managed SO references remain available for main-thread operations (UI, loot resolution, crafting).
- No further content additions are allowed (registries are frozen).

---

## Mod Content (AssetBundle-Based)

Mods are currently loaded as Unity **AssetBundles** with the `.lithmod` extension from `Application.persistentDataPath/mods/`.

`ModLoader.LoadAllMods()` (Phase 13):
1. Scans for `*.lithmod` files
2. Calls `AssetBundle.LoadFromFile()` for each
3. Calls `bundle.LoadAllAssets<T>()` for each content SO type
4. Loaded mod blocks are registered into the same `StateRegistry` after core content

Currently only mod blocks are wired into the pipeline. Other loaded types (items, biomes, etc.) are populated but not yet fully integrated beyond blocks.

---

## Settings Loading

`SettingsLoader.Load()` is called in `LithforgeBootstrap.Awake()` before content loading:

```csharp
Resources.Load<WorldGenSettings>("Settings/WorldGenSettings")
Resources.Load<ChunkSettings>("Settings/ChunkSettings")
Resources.Load<PhysicsSettings>("Settings/PhysicsSettings")
Resources.Load<RenderingSettings>("Settings/RenderingSettings")
Resources.Load<DebugSettings>("Settings/DebugSettings")
Resources.Load<GameplaySettings>("Settings/GameplaySettings")
```

`LoadOrCreate<T>()` fallback: if an `.asset` file is missing, calls `ScriptableObject.CreateInstance<T>()` to produce a transient default instance rather than crashing.

---

## Future: JSON-Based Mod Content (NOT YET IMPLEMENTED)

The following is planned but does not exist in code:

- JSON-based content files for mods (coexisting with SOs for core content)
- `mod.json` manifests with dependency declarations
- `game.json` manifests for complete games on the engine
- Filesystem-based content loading from `StreamingAssets/` + `persistentDataPath/`
- Topological sort of mod load order with cycle detection
- Tag merging modes (additive vs. replace)
- Content validation against expected schemas

The `Assets/Editor/Content/JsonToSOConverter.cs` exists as a utility to convert JSON definitions to ScriptableObjects, but runtime JSON loading is not part of the content pipeline.

---

## Invariants

1. After Phase 16, no new content can be registered. Registries are immutable.
2. `StateId(0)` is always AIR. This is hardcoded and cannot be overridden.
3. The `NativeStateRegistry` bake has exactly the same length and indexing as the managed `StateRegistry`.
4. `BiomeData[i].BiomeId == i` — biome lookup is O(1) by direct index.
5. Textures must be square and power-of-two (16×16 standard). Texture array layers ≤ 1024.
6. `LootFunction.PreParseValues()` is called at load time so hot-path resolution avoids string parsing.
7. Content loading runs on the main thread (coroutine-based), yielding frames between phases for LoadingScreen updates.
