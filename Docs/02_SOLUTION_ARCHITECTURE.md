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
│   │   │   ├── Data/
│   │   │   │   ├── ResourceId.cs              # readonly record struct "namespace:name"
│   │   │   │   ├── IRegistry.cs               # interface: Get, Contains, GetAll
│   │   │   │   ├── RegistryBuilder.cs          # mutable during content loading
│   │   │   │   ├── Registry.cs                 # frozen read-only after Build()
│   │   │   │   ├── DataDefinition.cs           # abstract base for all definitions
│   │   │   │   └── IDataLoader.cs              # JSON deserialization contract
│   │   │   ├── Serialization/
│   │   │   │   ├── IBinaryWriter.cs
│   │   │   │   ├── IBinaryReader.cs
│   │   │   │   ├── LithBinaryWriter.cs         # little-endian, span-based
│   │   │   │   └── LithBinaryReader.cs
│   │   │   ├── Events/
│   │   │   │   ├── IEventBus.cs                # Publish<T>, Subscribe<T>, Unsubscribe
│   │   │   │   └── EventBus.cs                 # managed, main-thread only, synchronous
│   │   │   ├── Logging/
│   │   │   │   ├── ILogger.cs
│   │   │   │   └── LogLevel.cs
│   │   │   └── Validation/
│   │   │       ├── ContentValidator.cs         # validates JSON against expected schema
│   │   │       ├── ValidationResult.cs         # errors, warnings list
│   │   │       └── ValidationPolicy.cs         # strict vs lenient
│   │   ├── Tests/
│   │   │   └── Lithforge.Core.Tests.asmdef
│   │   ├── package.json
│   │   └── Lithforge.Core.asmdef              # References: NOTHING
│   │
│   ├── com.lithforge.crafting/                # Tier 1
│   │   ├── Runtime/
│   │   │   ├── Inventory.cs                   # generic slot container
│   │   │   ├── RecipeDefinition.cs            # shaped, shapeless, smelting
│   │   │   ├── RecipeRegistry.cs
│   │   │   ├── RecipeType.cs
│   │   │   ├── CraftingEngine.cs              # validates and executes crafting
│   │   │   └── Ingredient.cs                  # item or tag reference
│   │   ├── Lithforge.Crafting.asmdef          # References: Core
│   │   └── package.json
│   │
│   ├── com.lithforge.modding/                 # Tier 1
│   │   ├── Runtime/
│   │   │   ├── ModManifest.cs                 # mod.json parsing
│   │   │   ├── ModRegistry.cs                 # loaded mods, load order
│   │   │   ├── ModContext.cs                  # scoped API access per mod
│   │   │   ├── ModLoadPhase.cs                # Discovery, Registration, Init, Ready
│   │   │   ├── ModDependencyResolver.cs       # topological sort with cycle detection
│   │   │   └── API/
│   │   │       ├── IBlockAPI.cs
│   │   │       ├── IItemAPI.cs
│   │   │       ├── IWorldGenAPI.cs
│   │   │       ├── IEntityAPI.cs
│   │   │       ├── ICraftingAPI.cs
│   │   │       ├── ITagAPI.cs
│   │   │       ├── ILootAPI.cs
│   │   │       └── IEventAPI.cs
│   │   ├── Lithforge.Modding.asmdef           # References: Core
│   │   └── package.json
│   │
│   │  ══════════ TIER 2 — Unity Core (Burst/Jobs/NativeContainers) ══════════
│   │
│   ├── com.lithforge.voxel/
│   │   ├── Runtime/
│   │   │   ├── Block/
│   │   │   │   ├── BlockDefinition.cs         # Tier 1 type (no Unity deps in this file)
│   │   │   │   ├── BlockRegistry.cs           # Registry<BlockDefinition>
│   │   │   │   ├── StateId.cs                 # readonly struct (ushort), blittable
│   │   │   │   ├── BlockState.cs              # managed resolved state
│   │   │   │   ├── BlockStateCompact.cs       # blittable struct for Burst (cached flags)
│   │   │   │   ├── StateRegistry.cs           # managed: builds state palette
│   │   │   │   ├── NativeStateRegistry.cs     # NativeArray<BlockStateCompact> bake for jobs
│   │   │   │   ├── BlockStateDefinition.cs    # blockstate JSON → variants/multipart
│   │   │   │   ├── Property/
│   │   │   │   │   ├── IPropertyValue.cs
│   │   │   │   │   ├── BoolProperty.cs
│   │   │   │   │   ├── IntProperty.cs
│   │   │   │   │   ├── EnumProperty.cs
│   │   │   │   │   └── PropertyDefinition.cs
│   │   │   │   └── Model/
│   │   │   │       ├── BlockModelDefinition.cs
│   │   │   │       ├── ModelElement.cs
│   │   │   │       ├── ModelFace.cs
│   │   │   │       ├── ModelVariant.cs
│   │   │   │       ├── MultipartCase.cs
│   │   │   │       ├── ModelRegistry.cs
│   │   │   │       └── ModelResolver.cs
│   │   │   ├── Chunk/
│   │   │   │   ├── ChunkConstants.cs          # Size=32, Volume=32768
│   │   │   │   ├── ChunkData.cs               # NativeArray<StateId>, palette compression
│   │   │   │   ├── ChunkState.cs              # enum: Unloaded→Generating→Generated→Meshing→Ready→Dirty
│   │   │   │   ├── ChunkNeighborData.cs       # NativeArray border slices from 6 neighbors
│   │   │   │   └── ChunkPool.cs               # pre-allocated NativeArray pool to avoid alloc/dealloc churn
│   │   │   ├── World/
│   │   │   │   ├── VoxelWorld.cs              # facade: GetBlock, SetBlock, GetChunk
│   │   │   │   ├── IChunkProvider.cs
│   │   │   │   └── ChunkManager.cs            # load/unload/save lifecycle
│   │   │   ├── Item/
│   │   │   │   ├── ItemDefinition.cs
│   │   │   │   ├── ItemRegistry.cs
│   │   │   │   ├── ItemStack.cs
│   │   │   │   └── ItemModelDefinition.cs
│   │   │   ├── Tag/
│   │   │   │   ├── TagDefinition.cs
│   │   │   │   ├── TagRegistry.cs
│   │   │   │   └── TagKey.cs
│   │   │   ├── Loot/
│   │   │   │   ├── LootTableDefinition.cs
│   │   │   │   ├── LootTableRegistry.cs
│   │   │   │   ├── LootCondition.cs
│   │   │   │   └── LootFunction.cs
│   │   │   ├── Fluid/
│   │   │   │   ├── FluidDefinition.cs
│   │   │   │   └── FluidState.cs
│   │   │   └── Storage/
│   │   │       ├── IWorldStorage.cs
│   │   │       ├── RegionFile.cs
│   │   │       ├── ChunkSerializer.cs
│   │   │       └── WorldMetadata.cs
│   │   ├── Lithforge.Voxel.asmdef             # References: Core, Unity.Collections, Unity.Mathematics, Unity.Burst
│   │   ├── Tests/
│   │   └── package.json
│   │
│   ├── com.lithforge.worldgen/                # Tier 2
│   │   ├── Runtime/
│   │   │   ├── Pipeline/
│   │   │   │   ├── IGenerationStage.cs        # managed interface (orchestrator is not Burst)
│   │   │   │   ├── GenerationPipeline.cs      # iterates stages, dispatches Burst jobs per stage
│   │   │   │   └── GenerationContext.cs       # NativeArray-backed shared state
│   │   │   ├── Stages/
│   │   │   │   ├── TerrainShapeJob.cs         # [BurstCompile] IJob
│   │   │   │   ├── CaveCarverJob.cs           # [BurstCompile] IJob
│   │   │   │   ├── BiomeAssignmentJob.cs      # [BurstCompile] IJob
│   │   │   │   ├── SurfaceBuilderJob.cs       # [BurstCompile] IJob
│   │   │   │   ├── OreGenerationJob.cs        # [BurstCompile] IJob
│   │   │   │   ├── DecorationStage.cs         # managed (structure placement crosses chunks)
│   │   │   │   └── InitialLightingJob.cs      # [BurstCompile] IJob
│   │   │   ├── Biome/
│   │   │   │   ├── BiomeDefinition.cs
│   │   │   │   ├── BiomeRegistry.cs
│   │   │   │   ├── NativeBiomeData.cs         # blittable bake for Burst
│   │   │   │   └── BiomeSelector.cs
│   │   │   ├── Noise/
│   │   │   │   ├── NoiseConfig.cs
│   │   │   │   ├── NativeNoiseProvider.cs     # Burst-compatible noise (Unity.Mathematics)
│   │   │   │   └── NoiseType.cs
│   │   │   ├── Ore/
│   │   │   │   ├── OreDefinition.cs
│   │   │   │   ├── OreType.cs
│   │   │   │   └── OrePlacerJob.cs            # [BurstCompile]
│   │   │   ├── Structure/
│   │   │   │   ├── StructureDefinition.cs
│   │   │   │   ├── StructureRegistry.cs
│   │   │   │   └── StructurePlacer.cs
│   │   │   └── Feature/
│   │   │       ├── ConfiguredFeature.cs
│   │   │       ├── PlacedFeature.cs
│   │   │       └── PlacementModifier.cs
│   │   ├── Lithforge.WorldGen.asmdef          # References: Core, Voxel, Unity.Collections, Unity.Mathematics, Unity.Burst, Unity.Jobs
│   │   └── package.json
│   │
│   ├── com.lithforge.meshing/                 # Tier 2
│   │   ├── Runtime/
│   │   │   ├── MeshData.cs                    # NativeList<MeshVertex> + NativeList<int> indices
│   │   │   ├── MeshVertex.cs                  # blittable struct matching VertexAttributeDescriptor
│   │   │   ├── ChunkMeshResult.cs             # opaque + cutout + translucent MeshData
│   │   │   ├── Greedy/
│   │   │   │   ├── GreedyMeshJob.cs           # [BurstCompile] IJob — full greedy pipeline
│   │   │   │   ├── GreedySlice.cs             # 32×32 face mask (NativeArray<uint>)
│   │   │   │   └── FaceMask.cs                # bitwise row merge utilities
│   │   │   ├── Custom/
│   │   │   │   ├── CustomModelMesher.cs       # emits geometry from model elements
│   │   │   │   ├── MultipartResolver.cs
│   │   │   │   └── FluidMesher.cs
│   │   │   ├── AO/
│   │   │   │   └── AmbientOcclusion.cs        # per-vertex AO (Burst-compatible static methods)
│   │   │   ├── Atlas/
│   │   │   │   ├── AtlasRegion.cs             # UV rect struct (blittable)
│   │   │   │   ├── NativeAtlasLookup.cs       # NativeArray<AtlasRegion> indexed by texture ID
│   │   │   │   └── AtlasBuilder.cs            # managed: builds atlas, produces NativeAtlasLookup
│   │   │   ├── LOD/
│   │   │   │   ├── LODLevel.cs
│   │   │   │   ├── LODMeshJob.cs              # [BurstCompile] simplified meshing
│   │   │   │   ├── LODManager.cs
│   │   │   │   └── LODConfig.cs
│   │   │   └── Culling/
│   │   │       ├── FrustumCuller.cs
│   │   │       ├── DistanceCuller.cs
│   │   │       └── OcclusionCuller.cs
│   │   ├── Lithforge.Meshing.asmdef           # References: Core, Voxel, Unity.Collections, Unity.Mathematics, Unity.Burst, Unity.Jobs
│   │   └── package.json
│   │
│   ├── com.lithforge.lighting/                # Tier 2
│   │   ├── Runtime/
│   │   │   ├── LightData.cs                   # NativeArray<byte> (packed 4+4 bits)
│   │   │   ├── LightPropagationJob.cs         # [BurstCompile] BFS with NativeQueue
│   │   │   ├── LightRemovalJob.cs             # [BurstCompile] BFS removal
│   │   │   ├── SunlightPropagationJob.cs      # [BurstCompile] top-down column
│   │   │   ├── LightEngine.cs                 # managed orchestrator: schedules jobs
│   │   │   └── LightConstants.cs              # MAX_LIGHT=15
│   │   ├── Lithforge.Lighting.asmdef          # References: Core, Voxel, Unity.Collections, Unity.Mathematics, Unity.Burst, Unity.Jobs
│   │   └── package.json
│   │
│   ├── com.lithforge.physics/                 # Tier 2
│   │   ├── Runtime/
│   │   │   ├── VoxelRaycast.cs                # DDA raycast (Burst-compatible static method)
│   │   │   ├── VoxelCollider.cs               # AABB vs voxel grid
│   │   │   ├── CollisionShapeType.cs          # enum: FullCube, Slab, Stairs, Fence, None
│   │   │   ├── CollisionShapeData.cs          # blittable AABB list per shape type
│   │   │   └── PhysicsConstants.cs            # GRAVITY, TERMINAL_VELOCITY
│   │   ├── Lithforge.Physics.asmdef           # References: Core, Voxel, Unity.Collections, Unity.Mathematics, Unity.Burst
│   │   └── package.json
│   │
│   ├── com.lithforge.entity/                  # Tier 2 (hybrid: definitions in Tier 1 types, runtime in DOTS)
│   │   ├── Runtime/
│   │   │   ├── Definition/
│   │   │   │   ├── EntityDefinition.cs        # managed, data-driven
│   │   │   │   └── EntityRegistry.cs
│   │   │   ├── Components/                    # DOTS IComponentData
│   │   │   │   ├── VoxelTransform.cs          # float3 position, quaternion rotation
│   │   │   │   ├── VoxelVelocity.cs           # float3 velocity
│   │   │   │   ├── Health.cs                  # int current, int max
│   │   │   │   ├── MobAI.cs                   # AI state enum + target entity
│   │   │   │   └── Lifetime.cs                # float remaining (for projectiles)
│   │   │   └── Systems/                       # DOTS ISystem
│   │   │       ├── MovementSystem.cs          # [BurstCompile] applies velocity + voxel collision
│   │   │       ├── GravitySystem.cs           # [BurstCompile]
│   │   │       ├── AISystem.cs
│   │   │       ├── DamageSystem.cs
│   │   │       ├── LifetimeSystem.cs          # [BurstCompile] destroys expired entities
│   │   │       └── SpawnSystem.cs
│   │   ├── Lithforge.Entity.asmdef            # References: Core, Voxel, Physics, Unity.Entities, Unity.Transforms, Unity.Collections, Unity.Mathematics, Unity.Burst
│   │   └── package.json
│   │
│   └── com.lithforge.network/                 # Tier 1/2 (deferred to V2)
│       ├── Runtime/
│       │   ├── PacketDefinition.cs
│       │   ├── PacketRegistry.cs
│       │   ├── Protocol/
│       │   │   ├── ChunkDataPacket.cs
│       │   │   ├── BlockChangePacket.cs
│       │   │   └── EntityUpdatePacket.cs
│       │   ├── Client/
│       │   │   └── NetworkClient.cs
│       │   └── Server/
│       │       ├── NetworkServer.cs
│       │       └── PlayerSession.cs
│       ├── Lithforge.Network.asmdef           # References: Core, Voxel, Unity.Collections
│       └── package.json
│
├── Assets/
│   │
│   │  ══════════ TIER 3 — Unity Runtime ══════════
│   │
│   ├── Lithforge.Runtime/
│   │   ├── Bootstrap/
│   │   │   ├── LithforgeBootstrap.cs          # [RuntimeInitializeOnLoadMethod] entry point
│   │   │   ├── InitializationSequence.cs      # creates all services in order
│   │   │   ├── ServiceContainer.cs            # simple DI, no framework dependency
│   │   │   └── GameLoop.cs                    # MonoBehaviour: Update/LateUpdate orchestration
│   │   ├── Rendering/
│   │   │   ├── ChunkRenderManager.cs          # manages chunk GameObjects lifecycle
│   │   │   ├── ChunkRenderer.cs               # MonoBehaviour: MeshFilter + MeshRenderer per chunk
│   │   │   ├── MeshUploader.cs                # Mesh.MeshDataArray → Mesh (main thread)
│   │   │   ├── VoxelMaterialManager.cs        # SharedMaterial instances for opaque/cutout/translucent
│   │   │   ├── TextureAtlasManager.cs         # builds Texture2DArray from content textures
│   │   │   ├── LODRenderer.cs                 # swaps mesh based on LOD level
│   │   │   ├── SkyController.cs               # procedural sky + day/night
│   │   │   ├── FogController.cs               # distance fog per biome
│   │   │   └── Shaders/
│   │   │       ├── LithforgeVoxelOpaque.shader     # URP-compatible, atlas sampling, AO, lighting
│   │   │       ├── LithforgeVoxelCutout.shader     # alpha test, double-sided
│   │   │       ├── LithforgeVoxelTranslucent.shader # alpha blend, water animation
│   │   │       └── LithforgeSky.shader              # atmospheric scattering
│   │   ├── UI/
│   │   │   ├── UIManager.cs                   # screen stack, transitions
│   │   │   ├── Screens/
│   │   │   │   ├── MainMenuScreen.cs/.uxml/.uss
│   │   │   │   ├── WorldSelectScreen.cs/.uxml/.uss
│   │   │   │   ├── WorldCreateScreen.cs/.uxml/.uss
│   │   │   │   ├── SettingsScreen.cs/.uxml/.uss
│   │   │   │   └── PauseMenuScreen.cs/.uxml/.uss
│   │   │   ├── HUD/
│   │   │   │   ├── CrosshairHUD.cs            # uGUI Canvas overlay
│   │   │   │   ├── HotbarHUD.cs
│   │   │   │   ├── HealthBarHUD.cs
│   │   │   │   ├── ChatHUD.cs
│   │   │   │   └── DebugOverlayHUD.cs         # IMGUI for performance counters
│   │   │   └── Inventory/
│   │   │       ├── InventoryScreen.cs/.uxml
│   │   │       ├── CraftingGridUI.cs
│   │   │       └── ItemSlotUI.cs
│   │   ├── Input/
│   │   │   ├── InputManager.cs                # InputSystem action map wrapper
│   │   │   ├── PlayerController.cs            # CharacterController + custom voxel collision
│   │   │   ├── CameraController.cs            # mouse look, FOV, head bob
│   │   │   ├── BlockInteraction.cs            # place/break via VoxelRaycast
│   │   │   └── LithforgeInputActions.inputactions  # Unity InputSystem asset
│   │   ├── Audio/
│   │   │   ├── AudioManager.cs                # AudioSource pool management
│   │   │   ├── BlockSoundPlayer.cs
│   │   │   └── AmbientSoundPlayer.cs
│   │   ├── Debug/
│   │   │   ├── ChunkBorderVisualizer.cs       # Gizmos wireframe
│   │   │   ├── NoisePreviewWindow.cs          # EditorWindow for noise visualization
│   │   │   ├── PerformanceMonitor.cs          # ProfilerMarker integration + IMGUI overlay
│   │   │   ├── BiomeMapVisualizer.cs
│   │   │   └── StateInspector.cs              # shows BlockState of targeted block
│   │   └── Lithforge.Runtime.asmdef           # References: ALL Tier 1+2 packages, UnityEngine, URP, UI Toolkit, InputSystem
│   │
│   ├── Content/                               # Data-driven content (loaded from StreamingAssets at runtime)
│   │   └── lithforge/
│   │       ├── assets/lithforge/
│   │       │   ├── blockstates/
│   │       │   ├── models/block/
│   │       │   ├── models/block/_parents/
│   │       │   ├── models/item/
│   │       │   ├── textures/block/
│   │       │   ├── textures/item/
│   │       │   ├── textures/colormap/
│   │       │   └── sounds/
│   │       └── data/lithforge/
│   │           ├── blocks/
│   │           ├── items/
│   │           ├── loot_tables/blocks/
│   │           ├── recipes/crafting/
│   │           ├── recipes/smelting/
│   │           ├── tags/blocks/
│   │           ├── tags/items/
│   │           └── worldgen/
│   │               ├── biome/
│   │               ├── configured_feature/
│   │               ├── placed_feature/
│   │               └── noise_settings/
│   │
│   ├── StreamingAssets/
│   │   └── content/ → symlink or copy of Content/ for runtime access
│   │
│   └── Settings/
│       ├── URPAsset.asset
│       ├── URPRenderer.asset
│       └── QualitySettings.asset
│
├── docs/
│   ├── 01_PROJECT_OVERVIEW.md
│   ├── 02_SOLUTION_ARCHITECTURE.md
│   ├── 03_VOXEL_CORE.md
│   ├── 04_MESHING_AND_RENDERING.md
│   ├── 05_WORLD_GENERATION.md
│   ├── 06_THREADING_AND_BURST.md
│   ├── 07_DATA_DRIVEN_CONTENT.md
│   ├── 08_REFERENCE_ANALYSIS.md
│   ├── 09_ROADMAP.md
│   ├── 10_ERROR_HANDLING.md
│   ├── 11_PLATFORM_ARCHITECTURE.md
│   ├── 12_OBSERVABILITY.md
│   ├── CLAUDE.md
│   └── adr/
│       ├── ADR-001_unity_over_godot.md
│       ├── ADR-002_three_tier_architecture.md
│       └── ...
│
├── .editorconfig
└── Lithforge.sln                              # Generated by Unity
```

---

## Dependency Graph

```
                    ┌──────────────────┐
                    │ Lithforge.Core   │  TIER 1
                    │ (pure C#)        │  depends on: NOTHING
                    └────────┬─────────┘
                             │
         ┌───────────────────┼───────────────────┐
         │                   │                   │
   ┌─────▼──────┐    ┌──────▼──────┐    ┌──────▼──────┐
   │  Crafting   │    │   Modding   │    │   Voxel     │  TIER 1 (data) + TIER 2 (native)
   │  (Tier 1)   │    │  (Tier 1)   │    │  (Tier 2)   │
   └─────────────┘    └─────────────┘    └──────┬──────┘
                                                 │
                    ┌────────────────┬────────────┼────────────┬──────────────┐
                    │                │            │            │              │
              ┌─────▼─────┐  ┌──────▼─────┐ ┌────▼────┐ ┌────▼─────┐ ┌─────▼─────┐
              │ WorldGen   │  │  Meshing   │ │Lighting │ │ Physics  │ │  Entity   │
              │ (Tier 2)   │  │ (Tier 2)   │ │(Tier 2) │ │(Tier 2)  │ │(Tier 2)   │
              └────────────┘  └────────────┘ └─────────┘ └──────────┘ └───────────┘
                                                                          │ (DOTS)
═══════════════════════════════ TIER 3 BOUNDARY ═══════════════════════════════════

                           ┌───────────────────┐
                           │ Lithforge.Runtime  │  TIER 3
                           │ (UnityEngine)      │  References ALL above
                           └─────────┬─────────┘
                                     │
              ┌──────────┬───────────┼───────────┬──────────┐
              │          │           │           │          │
          Rendering     UI        Input       Audio      Debug
```

---

## Assembly Definition Rules

| Assembly | Tier | May Reference | Must NOT Reference |
|----------|------|--------------|-------------------|
| `Lithforge.Core` | 1 | (nothing) | Any Unity namespace |
| `Lithforge.Crafting` | 1 | Core | Any Unity namespace |
| `Lithforge.Modding` | 1 | Core | Any Unity namespace |
| `Lithforge.Voxel` | 2 | Core, Unity.Collections, Unity.Mathematics, Unity.Burst | UnityEngine, Unity.Entities |
| `Lithforge.WorldGen` | 2 | Core, Voxel, Unity.Collections, Unity.Mathematics, Unity.Burst, Unity.Jobs | UnityEngine |
| `Lithforge.Meshing` | 2 | Core, Voxel, Unity.Collections, Unity.Mathematics, Unity.Burst, Unity.Jobs | UnityEngine |
| `Lithforge.Lighting` | 2 | Core, Voxel, Unity.Collections, Unity.Mathematics, Unity.Burst, Unity.Jobs | UnityEngine |
| `Lithforge.Physics` | 2 | Core, Voxel, Unity.Collections, Unity.Mathematics, Unity.Burst | UnityEngine |
| `Lithforge.Entity` | 2 | Core, Voxel, Physics, Unity.Entities, Unity.Transforms, Unity.Collections, Unity.Mathematics, Unity.Burst | UnityEngine (except for authoring) |
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
