using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Core.Logging;
using Lithforge.Core.Validation;
using Lithforge.Meshing.Atlas;
using Lithforge.Physics;
using Lithforge.Runtime.Content;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Spawn;
using Lithforge.Runtime.UI;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Item;
using Lithforge.Voxel.Loot;
using Lithforge.Voxel.Storage;
using Lithforge.WorldGen.Biome;
using Lithforge.WorldGen.Decoration;
using Lithforge.WorldGen.Noise;
using Lithforge.WorldGen.Ore;
using Lithforge.WorldGen.Pipeline;
using Unity.Collections;
using UnityEngine;

namespace Lithforge.Runtime.Bootstrap
{
    public sealed class LithforgeBootstrap : MonoBehaviour
    {
        [SerializeField] private Material voxelMaterial;

        private LoadedSettings _settings;
        private ServiceContainer _services;
        private ContentPipelineResult _contentResult;
        private ChunkPool _chunkPool;
        private ChunkManager _chunkManager;
        private GenerationPipeline _generationPipeline;
        private ChunkRenderManager _chunkRenderManager;
        private GameLoop _gameLoop;
        private NativeArray<NativeBiomeData> _nativeBiomeData;
        private NativeArray<NativeOreConfig> _nativeOreConfigs;
        private DecorationStage _decorationStage;
        private WorldStorage _worldStorage;
        private TimeOfDayController _timeOfDayController;

        private void Awake()
        {
            _settings = SettingsLoader.Load();
            _services = new ServiceContainer();

            InitializeContent();
            InitializeStorage();
            InitializeChunkSystem();
            InitializeWorldGen();
            InitializeRendering();
            InitializeGameLoop();

            UnityEngine.Debug.Log("[Lithforge] Bootstrap complete.");
        }

        private void InitializeContent()
        {
            UnityLogger logger = new UnityLogger();
            ContentValidator validator = new ContentValidator();

            ContentPipeline pipeline = new ContentPipeline(
                logger, validator, _settings.Rendering.AtlasTileSize);
            string contentRoot = System.IO.Path.Combine(
                Application.streamingAssetsPath, "content", "lithforge");

            _contentResult = pipeline.Build(contentRoot);

            _services.Register(_contentResult.StateRegistry);
            _services.Register(_contentResult.NativeStateRegistry);

            UnityEngine.Debug.Log(
                $"[Lithforge] Content pipeline: {_contentResult.StateRegistry.TotalStateCount} states, " +
                $"{_contentResult.NativeAtlasLookup.TextureCount} textures, " +
                $"{_contentResult.BiomeDefinitions.Length} biomes, " +
                $"{_contentResult.OreDefinitions.Length} ores, " +
                $"{_contentResult.ItemEntries.Count} items, " +
                $"{_contentResult.LootTables.Count} loot tables, " +
                $"{_contentResult.TagRegistry.TagCount} tags.");
        }

        private void InitializeStorage()
        {
            string worldDir = System.IO.Path.Combine(
                Application.persistentDataPath, "worlds", "default");
            _worldStorage = new WorldStorage(worldDir);
            _worldStorage.SaveMetadata(_settings.WorldGen.Seed, "");
            _services.Register(_worldStorage);

            UnityEngine.Debug.Log($"[Lithforge] World storage: {worldDir}");
        }

        private void InitializeChunkSystem()
        {
            ChunkSettings cs = _settings.Chunk;
            _chunkPool = new ChunkPool(cs.PoolSize);
            _chunkManager = new ChunkManager(
                _chunkPool,
                cs.RenderDistance,
                cs.YLoadMin,
                cs.YLoadMax,
                cs.YUnloadMin,
                cs.YUnloadMax);

            _services.Register(_chunkPool);
            _services.Register(_chunkManager);
        }

        private void InitializeWorldGen()
        {
            WorldGenSettings wg = _settings.WorldGen;

            NativeNoiseConfig terrainNoise = wg.TerrainNoise.ToNativeConfig();
            NativeNoiseConfig temperatureNoise = wg.TemperatureNoise.ToNativeConfig();
            NativeNoiseConfig humidityNoise = wg.HumidityNoise.ToNativeConfig();
            NativeNoiseConfig caveNoise = wg.CaveNoise.ToNativeConfig();

            StateId stoneId = FindStateId("lithforge:stone");
            StateId airId = StateId.Air;
            StateId waterId = FindStateId("lithforge:water");

            // Build native biome data
            BiomeDefinition[] biomes = _contentResult.BiomeDefinitions;
            _nativeBiomeData = new NativeArray<NativeBiomeData>(
                biomes.Length, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeDefinition def = biomes[i];
                _nativeBiomeData[i] = new NativeBiomeData
                {
                    BiomeId = (byte)i,
                    TemperatureMin = def.TemperatureMin,
                    TemperatureMax = def.TemperatureMax,
                    TemperatureCenter = def.TemperatureCenter,
                    HumidityMin = def.HumidityMin,
                    HumidityMax = def.HumidityMax,
                    HumidityCenter = def.HumidityCenter,
                    TopBlock = FindStateIdForBlock(def.TopBlock),
                    FillerBlock = FindStateIdForBlock(def.FillerBlock),
                    StoneBlock = FindStateIdForBlock(def.StoneBlock),
                    UnderwaterBlock = FindStateIdForBlock(def.UnderwaterBlock),
                    FillerDepth = (byte)def.FillerDepth,
                    TreeDensity = def.TreeDensity,
                    HeightModifier = def.HeightModifier,
                };
            }

            // Build native ore configs
            OreDefinition[] ores = _contentResult.OreDefinitions;
            _nativeOreConfigs = new NativeArray<NativeOreConfig>(
                ores.Length, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            for (int i = 0; i < ores.Length; i++)
            {
                OreDefinition def = ores[i];
                _nativeOreConfigs[i] = new NativeOreConfig
                {
                    OreStateId = FindStateIdForBlock(def.OreBlock),
                    ReplaceStateId = FindStateIdForBlock(def.ReplaceBlock),
                    MinY = def.MinY,
                    MaxY = def.MaxY,
                    VeinSize = def.VeinSize,
                    Frequency = def.Frequency,
                    OreType = (byte)(def.OreType == OreType.Scatter ? 0 : 1),
                };
            }

            _generationPipeline = new GenerationPipeline(
                terrainNoise,
                temperatureNoise,
                humidityNoise,
                caveNoise,
                wg.CaveThreshold,
                wg.MinCarveY,
                wg.CaveSeedOffset1,
                wg.CaveSeedOffset2,
                wg.SeaLevelCarveBuffer,
                _nativeBiomeData,
                _nativeOreConfigs,
                _contentResult.NativeStateRegistry.States,
                stoneId, airId, waterId,
                wg.SeaLevel);

            // Build decoration stage
            StateId oakLogId = FindStateId("lithforge:oak_log");
            StateId oakLeavesId = FindStateId("lithforge:oak_leaves");
            _decorationStage = new DecorationStage(_nativeBiomeData, oakLogId, oakLeavesId, airId);

            _services.Register(_generationPipeline);
        }

        private void InitializeRendering()
        {
            Material opaqueMaterial = voxelMaterial;

            if (opaqueMaterial == null)
            {
                Shader shader = Shader.Find("Lithforge/VoxelOpaque");

                if (shader == null)
                {
                    // Fallback to old shader
                    shader = Shader.Find("Lithforge/VoxelUnlit");
                }

                if (shader != null)
                {
                    opaqueMaterial = new Material(shader);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[Lithforge] VoxelOpaque shader not found, using default.");

                    opaqueMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                }
            }

            // Assign atlas texture to opaque material
            if (_contentResult.AtlasResult != null && _contentResult.AtlasResult.TextureArray != null)
            {
                opaqueMaterial.SetTexture("_AtlasArray", _contentResult.AtlasResult.TextureArray);
            }

            // Create translucent material
            Material translucentMaterial;
            Shader translucentShader = Shader.Find("Lithforge/VoxelTranslucent");

            if (translucentShader != null)
            {
                translucentMaterial = new Material(translucentShader);
            }
            else
            {
                UnityEngine.Debug.LogWarning("[Lithforge] VoxelTranslucent shader not found, using opaque fallback.");
                translucentMaterial = new Material(opaqueMaterial);
            }

            if (_contentResult.AtlasResult != null && _contentResult.AtlasResult.TextureArray != null)
            {
                translucentMaterial.SetTexture("_AtlasArray", _contentResult.AtlasResult.TextureArray);
            }

            _chunkRenderManager = new ChunkRenderManager(opaqueMaterial, translucentMaterial);
            _services.Register(_chunkRenderManager);
        }

        private void InitializeGameLoop()
        {
            // Create GameLoop first (needed by PlayerController for spawn readiness)
            _gameLoop = gameObject.AddComponent<GameLoop>();
            _gameLoop.Initialize(
                _chunkManager,
                _generationPipeline,
                _contentResult.NativeStateRegistry,
                _contentResult.NativeAtlasLookup,
                _chunkRenderManager,
                _decorationStage,
                _worldStorage,
                _settings.WorldGen.Seed,
                _settings.Chunk);

            // Create player object with camera as child
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
            {
                // Remove legacy FPSCameraController if present
                FPSCameraController legacyController = mainCamera.GetComponent<FPSCameraController>();

                if (legacyController != null)
                {
                    Destroy(legacyController);
                }

                // Create player parent object
                GameObject playerObject = new GameObject("Player");
                playerObject.transform.position = new Vector3(
                    0,
                    _settings.WorldGen.SeaLevel + _settings.Chunk.InitialSpawnYOffset,
                    0);

                // Reparent camera under player
                mainCamera.transform.SetParent(playerObject.transform);
                mainCamera.transform.localPosition = new Vector3(
                    0f, _settings.Physics.PlayerEyeHeight, 0f);
                mainCamera.transform.localRotation = Quaternion.Euler(
                    _settings.Rendering.InitialCameraPitch, 0f, 0f);
                mainCamera.farClipPlane = _settings.Rendering.FarClipPlane;

                // Add PlayerController to player object
                PlayerController playerController = playerObject.AddComponent<PlayerController>();
                playerController.Initialize(
                    _chunkManager, _contentResult.NativeStateRegistry,
                    _gameLoop, _settings.Physics);
                _services.Register(playerController);

                // Add CameraController to camera
                CameraController cameraController = mainCamera.gameObject.AddComponent<CameraController>();
                _services.Register(cameraController);

                // Add BlockHighlight (standalone object)
                GameObject highlightObject = new GameObject("BlockHighlight");
                BlockHighlight blockHighlight = highlightObject.AddComponent<BlockHighlight>();
                blockHighlight.Initialize(_settings.Rendering);

                // Create player inventory and loot resolver
                Inventory playerInventory = new Inventory();
                LootResolver lootResolver = new LootResolver(_contentResult.LootTables);

                // Grant starting items from GameplaySettings
                IReadOnlyList<StartingItemEntry> startingItems = _settings.Gameplay.StartingItems;

                for (int i = 0; i < startingItems.Count; i++)
                {
                    StartingItemEntry entry = startingItems[i];
                    ResourceId itemId = new ResourceId(entry.itemNamespace, entry.itemName);
                    ItemEntry itemDef = _contentResult.ItemRegistry.Get(itemId);
                    int maxStack = itemDef != null
                        ? itemDef.MaxStackSize
                        : _settings.Physics.DefaultMaxStackSize;
                    playerInventory.AddItem(itemId, entry.count, maxStack);
                }

                // Add BlockInteraction to camera (raycasts from camera position/direction)
                BlockInteraction blockInteraction = mainCamera.gameObject.AddComponent<BlockInteraction>();
                blockInteraction.Initialize(
                    _chunkManager,
                    _contentResult.NativeStateRegistry,
                    _contentResult.StateRegistry,
                    blockHighlight,
                    playerInventory,
                    _contentResult.ItemRegistry,
                    lootResolver,
                    _settings.Physics);
                _services.Register(blockInteraction);

                // Load shared PanelSettings asset for UI Toolkit
                UnityEngine.UIElements.PanelSettings panelSettings =
                    Resources.Load<UnityEngine.UIElements.PanelSettings>("DefaultPanelSettings");

                if (panelSettings == null)
                {
                    UnityEngine.Debug.LogError(
                        "[Lithforge] DefaultPanelSettings not found in Resources/. UI will not display.");
                }

                // Add CrosshairHUD
                GameObject crosshairObject = new GameObject("CrosshairHUD");
                CrosshairHUD crosshairHUD = crosshairObject.AddComponent<CrosshairHUD>();
                crosshairHUD.Initialize(panelSettings);

                // Add HotbarHUD
                GameObject hotbarObject = new GameObject("HotbarHUD");
                HotbarHUD hotbarHUD = hotbarObject.AddComponent<HotbarHUD>();
                hotbarHUD.Initialize(playerInventory, panelSettings);

                // Add InventoryScreen
                GameObject inventoryObject = new GameObject("InventoryScreen");
                InventoryScreen inventoryScreen = inventoryObject.AddComponent<InventoryScreen>();
                inventoryScreen.Initialize(
                    playerInventory,
                    _contentResult.ItemRegistry,
                    _contentResult.CraftingEngine,
                    panelSettings);
                _services.Register(inventoryScreen);

                // Add debug HUD
                DebugOverlayHUD debugHud = gameObject.AddComponent<DebugOverlayHUD>();
                debugHud.Initialize(_gameLoop, _chunkManager, _settings.Debug);

                // Hide all gameplay HUD until spawn is complete
                HudVisibilityController hudVisibility = new HudVisibilityController(
                    crosshairHUD, hotbarHUD, inventoryScreen, debugHud);
                hudVisibility.HideAll();

                // Create SpawnManager to coordinate chunk loading and player placement
                SpawnManager spawnManager = new SpawnManager(
                    _chunkManager,
                    _contentResult.NativeStateRegistry,
                    playerObject.transform,
                    _settings.Chunk.SpawnLoadRadius,
                    _settings.Chunk.YLoadMin,
                    _settings.Chunk.YLoadMax,
                    _settings.Chunk.SpawnFallbackY);
                _gameLoop.SetSpawnManager(spawnManager);

                // Create loading screen overlay — HUD is shown after the fade completes
                GameObject loadingObject = new GameObject("LoadingScreen");
                LoadingScreen loadingScreen = loadingObject.AddComponent<LoadingScreen>();
                loadingScreen.Initialize(
                    spawnManager,
                    panelSettings,
                    () => { hudVisibility.ShowGameplay(); });
            }

            // Initialize day/night cycle
            Material material = _chunkRenderManager.OpaqueMaterial;

            if (material != null)
            {
                _timeOfDayController = gameObject.AddComponent<TimeOfDayController>();
                _timeOfDayController.Initialize(
                    material,
                    _chunkRenderManager.TranslucentMaterial,
                    _settings.Rendering);
            }
        }

        private StateId FindStateIdForBlock(BlockDefinition blockDef)
        {
            if (blockDef == null)
            {
                return StateId.Air;
            }

            return FindStateId(blockDef.Namespace + ":" + blockDef.BlockName);
        }

        private StateId FindStateId(string idString)
        {
            if (string.IsNullOrEmpty(idString) || !idString.Contains(':'))
            {
                UnityEngine.Debug.LogWarning($"[Lithforge] Invalid block id '{idString}', returning AIR.");
                return StateId.Air;
            }

            string[] parts = idString.Split(':');
            string ns = parts[0];
            string name = parts[1];

            System.Collections.Generic.IReadOnlyList<StateRegistryEntry> entries =
                _contentResult.StateRegistry.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                StateRegistryEntry entry = entries[i];

                if (entry.Id.Namespace == ns && entry.Id.Name == name)
                {
                    return new StateId(entry.BaseStateId);
                }
            }

            UnityEngine.Debug.LogWarning($"[Lithforge] Block '{idString}' not found, returning AIR.");
            return StateId.Air;
        }

        private void OnDestroy()
        {
            if (_gameLoop != null)
            {
                _gameLoop.Shutdown();
            }

            // Save all loaded chunks before disposing
            if (_worldStorage != null && _chunkManager != null)
            {
                _chunkManager.SaveAllChunks(_worldStorage);
                _worldStorage.FlushAll();
                _worldStorage.SaveMetadata(_settings.WorldGen.Seed, "");
            }

            if (_chunkRenderManager != null)
            {
                _chunkRenderManager.Dispose();
            }

            if (_chunkManager != null)
            {
                _chunkManager.Dispose();
            }

            if (_chunkPool != null)
            {
                _chunkPool.Dispose();
            }

            if (_contentResult != null)
            {
                if (_contentResult.NativeStateRegistry.States.IsCreated)
                {
                    _contentResult.NativeStateRegistry.Dispose();
                }

                _contentResult.NativeAtlasLookup.Dispose();
            }

            if (_nativeBiomeData.IsCreated)
            {
                _nativeBiomeData.Dispose();
            }

            if (_nativeOreConfigs.IsCreated)
            {
                _nativeOreConfigs.Dispose();
            }

            if (_worldStorage != null)
            {
                _worldStorage.Dispose();
            }
        }
    }

}
