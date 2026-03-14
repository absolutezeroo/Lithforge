# Lithforge — Solution Architecture

## Package Structure

Lithforge is organized as Unity **local packages** (under `Packages/`) for engine code and `Assets/` for runtime integration and content. Each package has an Assembly Definition (`.asmdef`) enforcing dependency boundaries.

```
Lithforge/
│
├── Packages/
│   │
│   │  ══════════ TIER 1 — Pure C# (zero Unity dependency) ══════════
│   │
│   ├── com.lithforge.core/
│   │   ├── Runtime/
│   │   │   ├── Registry/
│   │   │   │   ├── Registry.cs                 # frozen read-only after Build()
│   │   │   │   ├── RegistryBuilder.cs          # mutable during content loading
│   │   │   │   └── IRegistry.cs                # interface: Get, Contains, GetAll
│   │   │   ├── Logging/
│   │   │   │   ├── ILogger.cs
│   │   │   │   ├── LogLevel.cs
│   │   │   │   └── NullLogger.cs
│   │   │   ├── Validation/
│   │   │   │   ├── ContentValidator.cs
│   │   │   │   └── ValidationResult.cs
│   │   │   └── Data/
│   │   │       └── ResourceId.cs               # readonly struct "namespace:name"
│   │   ├── Tests/
│   │   │   └── Lithforge.Core.Tests.asmdef
│   │   ├── package.json
│   │   └── Lithforge.Core.asmdef              # References: NOTHING, noEngineReferences: true
│   │
│   │  ══════════ TIER 2 — Unity Core (Burst/Jobs/NativeContainers) ══════════
│   │
│   ├── com.lithforge.voxel/
│   │   ├── Runtime/
│   │   │   ├── Block/
│   │   │   │   ├── StateId.cs                 # readonly struct (ushort), blittable
│   │   │   │   ├── BlockStateCompact.cs       # blittable struct for Burst (cached flags)
│   │   │   │   ├── BlockRegistrationData.cs   # managed block registration info
│   │   │   │   ├── StateRegistry.cs           # managed: builds state palette
│   │   │   │   ├── StateRegistryEntry.cs
│   │   │   │   ├── BlockMaterialType.cs       # enum: Opaque, Cutout, Translucent
│   │   │   │   └── NativeStateRegistry.cs     # NativeArray<BlockStateCompact> bake for jobs
│   │   │   ├── Chunk/
│   │   │   │   ├── ChunkConstants.cs          # Size=32, Volume=32768
│   │   │   │   ├── ChunkData.cs               # NativeArray<StateId>
│   │   │   │   ├── ChunkState.cs              # enum: Unloaded→Loading→Generating→Decorating→RelightPending→Generated→Meshing→Ready
│   │   │   │   ├── ManagedChunk.cs            # wrapper with LODLevel, BorderLightEntries, IsDirty
│   │   │   │   ├── ChunkBorderExtractor.cs    # extracts 32×32 border slices from neighbors
│   │   │   │   ├── ExtractSingleBorderJob.cs  # [BurstCompile] single-border extraction
│   │   │   │   ├── DeferredEdit.cs            # deferred block edit (applied after meshing)
│   │   │   │   ├── BorderLightEntry.cs        # managed border light entry
│   │   │   │   ├── ChunkPool.cs               # pre-allocated NativeArray pool
│   │   │   │   └── ChunkManager.cs            # load/unload/save lifecycle, forward-weighted queue sort
│   │   │   ├── Storage/
│   │   │   │   ├── WorldMetadata.cs           # world.json: seed, version, content_hash
│   │   │   │   ├── ChunkSerializer.cs         # palette + DeflateStream compression
│   │   │   │   ├── RegionFile.cs              # atomic write with .tmp/.bak rotation
│   │   │   │   └── WorldStorage.cs            # region-based chunk persistence
│   │   │   ├── Item/
│   │   │   │   ├── ToolType.cs
│   │   │   │   ├── ItemStack.cs
│   │   │   │   ├── ItemEntry.cs
│   │   │   │   ├── ItemRegistry.cs
│   │   │   │   ├── IMiningModifier.cs          # interface for mining speed modifiers
│   │   │   │   ├── MiningContext.cs            # context for mining speed calculation
│   │   │   │   └── Inventory.cs               # 36-slot player inventory
│   │   │   ├── Loot/
│   │   │   │   ├── LootTableDefinition.cs
│   │   │   │   ├── LootPool.cs
│   │   │   │   ├── LootEntry.cs
│   │   │   │   ├── LootCondition.cs
│   │   │   │   ├── LootFunction.cs
│   │   │   │   ├── LootDrop.cs
│   │   │   │   └── LootResolver.cs
│   │   │   ├── Tag/
│   │   │   │   ├── TagDefinition.cs
│   │   │   │   └── TagRegistry.cs             # bidirectional lookup
│   │   │   ├── Crafting/
│   │   │   │   ├── RecipeType.cs
│   │   │   │   ├── RecipeEntry.cs
│   │   │   │   ├── CraftingGrid.cs
│   │   │   │   └── CraftingEngine.cs
│   │   │   └── Jobs/
│   │   │       └── FillColumnJob.cs
│   │   ├── Lithforge.Voxel.asmdef             # References: Core, Unity.Collections, Unity.Mathematics, Unity.Burst
│   │   ├── Tests/
│   │   └── package.json
│   │
│   ├── com.lithforge.worldgen/
│   │   ├── Runtime/
│   │   │   ├── Pipeline/
│   │   │   │   ├── GenerationPipeline.cs      # schedules 7-stage Burst job chain
│   │   │   │   └── GenerationHandle.cs        # tracks in-flight generation + temp NativeArrays
│   │   │   ├── Stages/
│   │   │   │   ├── TerrainShapeJob.cs         # [BurstCompile] IJob — 2D heightmap
│   │   │   │   ├── CaveCarverJob.cs           # [BurstCompile] IJob — spaghetti caves
│   │   │   │   ├── ClimateNoiseJob.cs         # [BurstCompile] IJob — temperature/humidity noise
│   │   │   │   ├── SurfaceBuilderJob.cs       # [BurstCompile] IJob
│   │   │   │   ├── OreGenerationJob.cs        # [BurstCompile] IJob — blob/scatter
│   │   │   │   ├── InitialLightingJob.cs      # [BurstCompile] IJob
│   │   │   │   ├── LightPropagationJob.cs     # [BurstCompile] IJob — BFS flood fill
│   │   │   │   ├── LightRemovalJob.cs         # [BurstCompile] IJob — BFS removal
│   │   │   │   └── LightUpdateJob.cs          # [BurstCompile] IJob — cross-chunk updates
│   │   │   ├── Decoration/
│   │   │   │   ├── DecorationStage.cs         # managed: per-biome tree placement
│   │   │   │   ├── TreeTemplate.cs            # 3 variants: OakTree, BirchTree, SpruceTree
│   │   │   │   ├── TreeBlock.cs
│   │   │   │   ├── PendingBlock.cs
│   │   │   │   └── PendingDecorationStore.cs  # cross-chunk pending blocks
│   │   │   ├── Biome/
│   │   │   │   └── NativeBiomeData.cs         # blittable: climate ranges, surface blocks, TreeTemplateIndex
│   │   │   ├── Ore/
│   │   │   │   └── NativeOreConfig.cs         # blittable ore generation params
│   │   │   ├── Noise/
│   │   │   │   ├── NativeNoise.cs             # Burst-compatible FBM noise (Unity.Mathematics)
│   │   │   │   └── NativeNoiseConfig.cs       # blittable noise parameters
│   │   │   ├── Climate/
│   │   │   │   └── ClimateData.cs             # blittable climate data struct
│   │   │   └── Lighting/
│   │   │       ├── LightUtils.cs              # nibble pack/unpack helpers
│   │   │       ├── LightBfs.cs                # unified BFS (propagation + removal + border)
│   │   │       └── NativeBorderLightEntry.cs  # blittable border light entry
│   │   ├── Lithforge.WorldGen.asmdef          # References: Core, Voxel, Unity.Collections, Unity.Mathematics, Unity.Burst, Unity.Jobs
│   │   ├── Tests/
│   │   └── package.json
│   │
│   ├── com.lithforge.meshing/
│   │   ├── Runtime/
│   │   │   ├── MeshData.cs                    # NativeList<MeshVertex> + NativeList<int>
│   │   │   ├── MeshVertex.cs                  # 40-byte blittable vertex struct
│   │   │   ├── VoxelAO.cs                     # per-vertex AO (Burst-compatible)
│   │   │   ├── GreedyMeshJob.cs               # [BurstCompile] IJob — binary greedy meshing
│   │   │   ├── GreedyMeshData.cs              # TempJob containers for greedy mesh flight
│   │   │   ├── CulledMeshJob.cs               # [BurstCompile] IJob — simple face culling
│   │   │   ├── VoxelDownsampleJob.cs          # [BurstCompile] IJob — majority-vote downsample
│   │   │   ├── LODGreedyMeshJob.cs            # [BurstCompile] IJob — greedy meshing for LOD
│   │   │   ├── LODMeshJob.cs                  # [BurstCompile] IJob — culled faces for LOD
│   │   │   ├── LODMeshData.cs                 # TempJob containers for LOD mesh flight
│   │   │   ├── PackedMeshVertex.cs            # 16-byte blittable vertex (4×uint32)
│   │   │   └── Atlas/
│   │   │       ├── AtlasEntry.cs
│   │   │       └── NativeAtlasLookup.cs       # NativeArray indexed by texture ID
│   │   ├── Lithforge.Meshing.asmdef           # References: Core, Voxel, Unity.Collections, Unity.Mathematics, Unity.Burst, Unity.Jobs
│   │   ├── Tests/
│   │   └── package.json
│   │
│   └── com.lithforge.physics/
│       ├── Runtime/
│       │   ├── VoxelRaycast.cs                # DDA raycast (Burst-compatible static method)
│       │   ├── VoxelCollider.cs               # AABB vs voxel grid
│       │   ├── Aabb.cs
│       │   ├── CollisionResult.cs
│       │   ├── RaycastHit.cs
│       │   └── PhysicsConstants.cs
│       ├── Lithforge.Physics.asmdef           # References: Core, Voxel, Unity.Collections, Unity.Mathematics, Unity.Burst
│       └── package.json
│
├── Assets/
│   │
│   │  ══════════ TIER 3 — Unity Runtime ══════════
│   │
│   ├── Lithforge.Runtime/
│   │   ├── GameLoop.cs                        # MonoBehaviour: per-frame orchestration of all schedulers
│   │   ├── Bootstrap/
│   │   │   ├── LithforgeBootstrap.cs          # MonoBehaviour entry point (Awake + Start coroutine)
│   │   │   ├── ContentPipeline.cs             # 14-phase IEnumerable<string> content loading
│   │   │   ├── ContentPipelineResult.cs       # aggregates all loaded content
│   │   │   └── UnityLogger.cs                 # ILogger → Debug.Log bridge
│   │   ├── Scheduling/
│   │   │   ├── GenerationScheduler.cs         # generation scheduling, cross-chunk light updates
│   │   │   ├── MeshScheduler.cs               # LOD0 greedy meshing, relight gating
│   │   │   ├── LODScheduler.cs                # LOD1-3 downsample + mesh, level transitions
│   │   │   ├── RelightScheduler.cs            # relight scheduling, circuit breaker
│   │   │   └── FrameBudget.cs                 # per-frame time budget tracking
│   │   ├── Rendering/
│   │   │   ├── ChunkMeshStore.cs              # manages per-chunk mesh regions in MegaMeshBuffer
│   │   │   ├── MegaMeshBuffer.cs              # single GPU mesh, sub-mesh regions per chunk
│   │   │   ├── ChunkBoundsGPU.cs              # uploads chunk AABBs for GPU culling
│   │   │   ├── HiZPyramid.cs                  # hierarchical-Z occlusion pyramid
│   │   │   ├── BiomeTintManager.cs            # per-biome grass/foliage/water tint colors
│   │   │   ├── ChunkCulling.cs                # frustum plane extraction + AABB test
│   │   │   ├── BlockHighlight.cs              # LineRenderer wireframe on targeted block
│   │   │   ├── SkyController.cs               # procedural sky + fog + ambient from TimeOfDay
│   │   │   ├── TimeOfDayController.cs         # cosine cycle, drives _SunLightFactor
│   │   │   ├── Atlas/
│   │   │   │   ├── AtlasBuilder.cs            # builds Texture2DArray from face textures
│   │   │   │   └── AtlasResult.cs
│   │   │   └── Shaders/
│   │   │       ├── FrustumCull.compute
│   │   │       ├── HiZGenerate.compute
│   │   │       ├── LithforgeVoxelOpaque.shader
│   │   │       ├── LithforgeVoxelCutout.shader
│   │   │       ├── LithforgeVoxelCommon.hlsl
│   │   │       └── LithforgeSky.shader
│   │   ├── Input/
│   │   │   ├── PlayerController.cs            # gravity, WASD, voxel collision, step-up
│   │   │   ├── CameraController.cs            # mouse look
│   │   │   ├── FPSCameraController.cs
│   │   │   ├── BlockInteraction.cs            # progressive mining, placement from inventory
│   │   │   └── SolidBlockQuery.cs
│   │   ├── UI/
│   │   │   ├── Screens/
│   │   │   │   ├── ContainerScreen.cs         # abstract base for inventory-style screens
│   │   │   │   ├── PlayerInventoryScreen.cs   # concrete player inventory (E-key toggle)
│   │   │   │   ├── HotbarDisplay.cs           # read-only hotbar display
│   │   │   │   └── SettingsScreen.cs          # UI Toolkit, Escape key, live-apply sliders
│   │   │   ├── Widgets/
│   │   │   │   ├── SlotWidget.cs              # icon + count + durability bar
│   │   │   │   ├── TooltipWidget.cs
│   │   │   │   ├── DragGhostWidget.cs
│   │   │   │   └── ItemNameBanner.cs
│   │   │   ├── Interaction/
│   │   │   │   ├── SlotInteractionController.cs  # 6 interaction modes
│   │   │   │   └── HeldStack.cs
│   │   │   ├── Container/
│   │   │   │   ├── ISlotContainer.cs
│   │   │   │   ├── InventoryContainerAdapter.cs
│   │   │   │   ├── CraftingGridContainerAdapter.cs
│   │   │   │   ├── CraftingOutputContainerAdapter.cs
│   │   │   │   └── SlotContainerContext.cs
│   │   │   ├── Layout/
│   │   │   │   ├── SlotGroupDefinition.cs
│   │   │   │   └── ContainerLayoutSO.cs
│   │   │   ├── Sprites/
│   │   │   │   ├── ItemSpriteAtlas.cs
│   │   │   │   └── ItemSpriteAtlasBuilder.cs
│   │   │   ├── CrosshairHUD.cs
│   │   │   ├── LoadingScreen.cs               # UI Toolkit, sortingOrder=500, progress bar + fade
│   │   │   ├── HudVisibilityController.cs     # hides HUDs during loading
│   │   │   ├── ItemDisplayFormatter.cs
│   │   │   └── Resources/DefaultPanelSettings.asset
│   │   ├── Spawn/
│   │   │   ├── SpawnManager.cs                # Minecraft-style safe spawn finding
│   │   │   ├── SpawnState.cs                  # Checking → FindingY → Teleporting → Done
│   │   │   └── SpawnProgress.cs
│   │   ├── Debug/
│   │   │   ├── DebugOverlayHUD.cs             # FPS, loaded chunks, renderer count, LOD queue
│   │   │   ├── FrameProfiler.cs               # Stopwatch-based, 300-frame rolling history
│   │   │   ├── PipelineStats.cs               # per-frame + cumulative counters
│   │   │   └── BenchmarkRunner.cs             # F5 trigger, CSV export, fly-through benchmark
│   │   ├── Content/
│   │   │   ├── Blocks/                        # BlockDefinition.cs, BlockStateMapping.cs, BlockBehavior.cs
│   │   │   ├── Items/                         # ItemDefinition.cs (ScriptableObject)
│   │   │   │   ├── Affixes/
│   │   │   │   │   ├── AffixDefinition.cs
│   │   │   │   │   ├── AffixEffectType.cs
│   │   │   │   │   └── AffixMiningEffect.cs
│   │   │   │   └── Enchantments/
│   │   │   │       ├── EnchantmentDefinition.cs
│   │   │   │       ├── EnchantmentCategory.cs
│   │   │   │       └── EnchantmentLevelData.cs
│   │   │   ├── Loot/                          # LootTable.cs (ScriptableObject)
│   │   │   ├── Models/                        # BlockModel.cs, ContentModelResolver.cs
│   │   │   ├── Recipes/                       # RecipeDefinition.cs (ScriptableObject)
│   │   │   ├── Tags/                          # Tag.cs (ScriptableObject)
│   │   │   ├── Tools/
│   │   │   │   └── ToolSpeedProfile.cs
│   │   │   ├── WorldGen/                      # BiomeDefinition.cs, OreDefinition.cs (SOs)
│   │   │   ├── Mods/                          # ModLoader.cs (AssetBundle-based .lithmod loading)
│   │   │   ├── Behaviors/
│   │   │   │   ├── BehaviorAction.cs
│   │   │   │   ├── GiveItemAction.cs
│   │   │   │   ├── PlaySoundAction.cs
│   │   │   │   ├── SetBlockAction.cs
│   │   │   │   ├── SpawnEntityAction.cs
│   │   │   │   └── SpawnParticleAction.cs
│   │   │   └── Settings/                      # SettingsLoader.cs, ChunkSettings.cs, etc. (SOs)
│   │   └── Lithforge.Runtime.asmdef           # References: ALL Tier 1+2 packages, UnityEngine, URP, InputSystem
│   │
│   ├── Editor/
│   │   ├── Lithforge.Editor.asmdef            # Editor-only, references Core + Voxel + Runtime
│   │   ├── Content/
│   │   │   ├── BlockModelEditor.cs
│   │   │   ├── CreateFullBlockSetup.cs
│   │   │   ├── BlockDefinitionEditor.cs
│   │   │   ├── ContentDashboard.cs
│   │   │   └── JsonToSOConverter.cs
│   │   └── Settings/                          # SettingsAssetCreator
│   │
│   ├── Resources/
│   │   ├── Content/                           # ScriptableObject assets loaded via Resources.LoadAll<T>()
│   │   │   ├── Blocks/                        # 19x BlockDefinition.asset
│   │   │   ├── BlockStates/                   # 19x BlockStateMapping.asset
│   │   │   ├── Models/                        # 16x BlockModel.asset (cube, cube_all, cube_column, etc.)
│   │   │   ├── Items/                         # 21x ItemDefinition.asset
│   │   │   ├── ItemModels/                    # ~17x BlockModel.asset
│   │   │   ├── LootTables/                    # ~21x LootTable.asset
│   │   │   ├── Tags/                          # 6x Tag.asset
│   │   │   ├── Recipes/                       # 13x RecipeDefinition.asset
│   │   │   ├── Biomes/                        # 4x BiomeDefinition.asset
│   │   │   ├── Ores/                          # 4x OreDefinition.asset
│   │   │   ├── Layouts/                       # ContainerLayoutSO assets
│   │   │   └── Textures/
│   │   │       ├── Blocks/                    # ~30x Texture2D PNG
│   │   │       └── Items/                     # ~27x Texture2D PNG
│   │   └── Settings/                          # 6x Settings ScriptableObject assets
│   │       ├── ChunkSettings.asset
│   │       ├── WorldGenSettings.asset
│   │       ├── RenderingSettings.asset
│   │       ├── PhysicsSettings.asset
│   │       ├── GameplaySettings.asset
│   │       └── DebugSettings.asset
│   │
│   └── Settings/
│       ├── URPAsset.asset
│       ├── URPRenderer.asset
│       └── QualitySettings.asset
│
├── Docs/
│   ├── 01_PROJECT_OVERVIEW.md ... 12_OBSERVABILITY.md
│   └── adr/
│
├── Sources/                                   # Reference implementations (git-ignored)
│   ├── luanti-master/                         # C++ voxel engine (Minetest fork)
│   └── Minosoft-master/                       # Kotlin Minecraft reimplementation
│
├── CLAUDE.md                                  # AI development context (project root)
├── .editorconfig
└── Lithforge.sln                              # Generated by Unity
```

### Planned Packages (NOT YET IMPLEMENTED)

The following packages are planned but do not exist in the codebase yet:

| Package | Tier | Purpose | Current Location |
|---------|------|---------|-----------------|
| `com.lithforge.crafting` | 1 | Separate crafting package | CraftingEngine lives in com.lithforge.voxel/Runtime/Crafting/ |
| `com.lithforge.modding` | 1 | Mod manifest parsing, load order, APIs | ModLoader lives in Assets/Lithforge.Runtime/Content/Mods/ |
| `com.lithforge.lighting` | 2 | Standalone light engine | Light jobs live in com.lithforge.worldgen/Runtime/Stages/ |
| `com.lithforge.entity` | 2 | DOTS ECS entities (mobs, NPCs) | Not started |
| `com.lithforge.network` | 1/2 | Client-server multiplayer | Not started |

---

## Dependency Graph

```
                    ┌──────────────────┐
                    │ Lithforge.Core   │  TIER 1
                    │ (pure C#)        │  depends on: NOTHING
                    └────────┬─────────┘
                             │
                    ┌────────▼─────────┐
                    │   Voxel          │  TIER 2
                    │   (Tier 2)       │  + Burst, Collections, Math, Newtonsoft.Json
                    └────────┬─────────┘
                             │
          ┌──────────────────┼──────────────────┐
          │                  │                  │
    ┌─────▼─────┐    ┌──────▼─────┐    ┌──────▼──────┐
    │ WorldGen   │    │  Meshing   │    │  Physics    │
    │ (Tier 2)   │    │ (Tier 2)   │    │ (Tier 2)    │
    │ + Jobs     │    │ + Jobs     │    │ (no Jobs)   │
    └────────────┘    └────────────┘    └─────────────┘

═══════════════════════ TIER 3 BOUNDARY ═══════════════════

                    ┌───────────────────┐
                    │ Lithforge.Runtime  │  TIER 3
                    │ (UnityEngine)      │  References ALL above
                    └─────────┬─────────┘
                              │
        ┌──────────┬──────────┼──────────┬──────────┐
        │          │          │          │          │
    Bootstrap  Scheduling  Rendering    UI      Content
                              │
                        ┌─────┼─────┐
                        │     │     │
                     Input  Spawn  Debug
```

---

## Assembly Definition Rules

| Assembly | Tier | May Reference | Must NOT Reference |
|----------|------|--------------|-------------------|
| `Lithforge.Core` | 1 | (nothing) | Any Unity namespace |
| `Lithforge.Voxel` | 2 | Core, Unity.Collections, Unity.Mathematics, Unity.Burst, Newtonsoft.Json | UnityEngine |
| `Lithforge.WorldGen` | 2 | Core, Voxel, Unity.Collections, Unity.Mathematics, Unity.Burst, Unity.Jobs | UnityEngine |
| `Lithforge.Meshing` | 2 | Core, Voxel, Unity.Collections, Unity.Mathematics, Unity.Burst, Unity.Jobs | UnityEngine |
| `Lithforge.Physics` | 2 | Core, Voxel, Unity.Collections, Unity.Mathematics, Unity.Burst | UnityEngine |
| `Lithforge.Runtime` | 3 | ALL packages, UnityEngine, URP, InputSystem, UIToolkit | (no restrictions) |

---

## Key Conventions

### .editorconfig

```ini
[*.cs]
csharp_new_line_before_open_brace = all
csharp_style_var_for_built_in_types = false:error
csharp_style_var_when_type_is_apparent = false:error
csharp_style_var_elsewhere = false:error
csharp_style_expression_bodied_methods = false:warning
dotnet_style_require_accessibility_modifiers = always:error
```

### Burst-Compatible Code Conventions (Tier 2)

```
- Use Unity.Mathematics types (float3, int3, math.sin) not System.Math
- Use NativeArray<T>, NativeList<T>, NativeQueue<T> not managed collections
- No class references in [BurstCompile] jobs
- No string operations in Burst paths
- No try/catch in Burst paths
- No interface dispatch in Burst paths
- All job structs marked with [BurstCompile]
- Job inputs marked [ReadOnly] when not modified
- Allocator.TempJob for per-frame data, Allocator.Persistent for long-lived data
```
