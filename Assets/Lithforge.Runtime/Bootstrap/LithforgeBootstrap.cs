using System.Collections;
using System.Collections.Generic;
using Lithforge.Core.Data;
using Lithforge.Core.Logging;
using Lithforge.Core.Validation;
using Lithforge.Meshing.Atlas;
using Lithforge.Physics;
using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.BlockEntity.UI;
using Lithforge.Runtime.Content.Blocks;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Content.WorldGen;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Player;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Spawn;
using Lithforge.Runtime.UI;
using Lithforge.Runtime.UI.Screens;
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
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Bootstrap
{
    public sealed class LithforgeBootstrap : MonoBehaviour
    {
        [FormerlySerializedAs("voxelMaterial")]
        [SerializeField] private Material _voxelMaterial;
        [FormerlySerializedAs("frustumCullShader")]
        [SerializeField] private ComputeShader _frustumCullShader;
        [FormerlySerializedAs("hiZGenerateShader")]
        [SerializeField] private ComputeShader _hiZGenerateShader;

        private LoadedSettings _settings;
        private ContentPipelineResult _contentResult;
        private ChunkPool _chunkPool;
        private ChunkManager _chunkManager;
        private GenerationPipeline _generationPipeline;
        private ChunkMeshStore _chunkMeshStore;
        private GameLoop _gameLoop;
        private NativeArray<NativeBiomeData> _nativeBiomeData;
        private NativeArray<NativeOreConfig> _nativeOreConfigs;
        private DecorationStage _decorationStage;
        private WorldStorage _worldStorage;
        private Lithforge.Core.Logging.ILogger _logger;
        private TimeOfDayController _timeOfDayController;
        private SkyController _skyController;
        private BiomeTintManager _biomeTintManager;

        private void Awake()
        {
            _settings = SettingsLoader.Load();
            _logger = new UnityLogger();

            // Initialize profiling systems
            FrameProfiler.Init();
            FrameProfiler.Enabled = _settings.Debug.EnableProfiling;
            PipelineStats.Enabled = _settings.Debug.EnableProfiling;
        }

        private IEnumerator Start()
        {
            // Create loading screen early so content phases are visible
            UnityEngine.UIElements.PanelSettings earlyPanelSettings =
                Resources.Load<UnityEngine.UIElements.PanelSettings>("DefaultPanelSettings");

            GameObject loadingObject = new GameObject("LoadingScreen");
            LoadingScreen loadingScreen = loadingObject.AddComponent<LoadingScreen>();
            loadingScreen.Initialize(null, earlyPanelSettings, null);

            // Run content pipeline as iterator, yielding frames between phases
            ContentValidator validator = new ContentValidator();
            ContentPipeline pipeline = new ContentPipeline(
                _logger, validator, _settings.Rendering.AtlasTileSize);

            foreach (string phase in pipeline.Build())
            {
                loadingScreen.SetContentPhase(phase);
                yield return null;
            }

            _contentResult = pipeline.Result;

            if (_contentResult == null)
            {
                UnityEngine.Debug.LogError("[Lithforge] Content pipeline failed — Result is null. Aborting bootstrap.");
                yield break;
            }

            UnityEngine.Debug.Log(
                $"[Lithforge] Content pipeline: {_contentResult.StateRegistry.TotalStateCount} states, " +
                $"{_contentResult.NativeAtlasLookup.TextureCount} textures, " +
                $"{_contentResult.BiomeDefinitions.Length} biomes, " +
                $"{_contentResult.OreDefinitions.Length} ores, " +
                $"{_contentResult.ItemEntries.Count} items, " +
                $"{_contentResult.LootTables.Count} loot tables, " +
                $"{_contentResult.TagRegistry.TagCount} tags.");

            InitializeStorage();
            InitializeChunkSystem();
            InitializeWorldGen();
            InitializeRendering();
            InitializeGameLoop(loadingScreen, earlyPanelSettings);

            UnityEngine.Debug.Log("[Lithforge] Bootstrap complete.");
        }

        private void InitializeStorage()
        {
            string worldDir = System.IO.Path.Combine(
                Application.persistentDataPath, "worlds", "default");
            _worldStorage = new WorldStorage(worldDir, _logger);
            _worldStorage.SaveMetadata(_settings.WorldGen.Seed, "");
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

        }

        private void InitializeWorldGen()
        {
            WorldGenSettings wg = _settings.WorldGen;

            NativeNoiseConfig terrainNoise = wg.TerrainNoise.ToNativeConfig();
            NativeNoiseConfig temperatureNoise = wg.TemperatureNoise.ToNativeConfig();
            NativeNoiseConfig humidityNoise = wg.HumidityNoise.ToNativeConfig();
            NativeNoiseConfig continentalnessNoise = wg.ContinentalnessNoise.ToNativeConfig();
            NativeNoiseConfig erosionNoise = wg.ErosionNoise.ToNativeConfig();
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
                    TreeTemplateIndex = (byte)def.TreeType,
                    ContinentalnessCenter = def.ContinentalnessCenter,
                    ErosionCenter = def.ErosionCenter,
                    BaseHeight = def.BaseHeight,
                    HeightAmplitude = def.HeightAmplitude,
                    WaterColorPacked = PackColor(def.WaterColor),
                    WeightSharpness = def.WeightSharpness,
                    SurfaceFlags = BuildSurfaceFlags(def),
                };
            }

            // Verify BiomeData[i].BiomeId == i invariant (required for O(1) lookup)
            for (int i = 0; i < biomes.Length; i++)
            {
                UnityEngine.Debug.Assert(
                    _nativeBiomeData[i].BiomeId == i,
                    $"[Lithforge] BiomeData invariant violated: BiomeData[{i}].BiomeId == {_nativeBiomeData[i].BiomeId}, expected {i}.");
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

            StateId iceId = FindStateId("lithforge:ice");
            StateId gravelId = FindStateId("lithforge:gravel");
            StateId sandId = FindStateId("lithforge:sand");
            Lithforge.WorldGen.River.NativeRiverConfig riverConfig = wg.RiverNoise.ToNativeConfig();

            _generationPipeline = new GenerationPipeline(
                terrainNoise,
                temperatureNoise,
                humidityNoise,
                continentalnessNoise,
                erosionNoise,
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
                iceId, gravelId, sandId,
                wg.SeaLevel,
                riverConfig);

            // Build decoration stage
            StateId oakLogId = FindStateId("lithforge:oak_log");
            StateId oakLeavesId = FindStateId("lithforge:oak_leaves");
            _decorationStage = new DecorationStage(_nativeBiomeData, oakLogId, oakLeavesId, airId, wg.SeaLevel);

        }

        private void InitializeRendering()
        {
            Material opaqueMaterial = _voxelMaterial;

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

            // Create cutout material for vegetation (alpha test, double-sided)
            Material cutoutMaterial;
            Shader cutoutShader = Shader.Find("Lithforge/VoxelCutout");

            if (cutoutShader != null)
            {
                cutoutMaterial = new Material(cutoutShader);
            }
            else
            {
                UnityEngine.Debug.LogWarning("[Lithforge] VoxelCutout shader not found, using opaque fallback.");
                cutoutMaterial = new Material(opaqueMaterial);
            }

            if (_contentResult.AtlasResult != null && _contentResult.AtlasResult.TextureArray != null)
            {
                cutoutMaterial.SetTexture("_AtlasArray", _contentResult.AtlasResult.TextureArray);
            }

            // Load frustum cull compute shader from Resources if not assigned in Inspector
            ComputeShader cullShader = _frustumCullShader;

            if (cullShader == null)
            {
                cullShader = Resources.Load<ComputeShader>("FrustumCull");
            }

            if (cullShader == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[Lithforge] FrustumCull compute shader not found. " +
                    "GPU frustum culling will be disabled.");
            }

            // Load Hi-Z pyramid generation compute shader
            ComputeShader hiZShader = _hiZGenerateShader;

            if (hiZShader == null)
            {
                hiZShader = Resources.Load<ComputeShader>("HiZGenerate");
            }

            if (hiZShader == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[Lithforge] HiZGenerate compute shader not found. " +
                    "Hi-Z occlusion culling will be disabled.");
            }

            _chunkMeshStore = new ChunkMeshStore(
                opaqueMaterial, cutoutMaterial, translucentMaterial,
                _settings.Chunk.RenderDistance,
                _settings.Chunk.YLoadMin,
                _settings.Chunk.YLoadMax,
                _settings.Chunk.YUnloadMin,
                _settings.Chunk.YUnloadMax,
                cullShader,
                hiZShader);

            // Build water color array indexed by biomeId
            BiomeDefinition[] biomes = _contentResult.BiomeDefinitions;
            Color[] biomeWaterColors = new Color[biomes.Length];

            for (int i = 0; i < biomes.Length; i++)
            {
                biomeWaterColors[i] = biomes[i].WaterColor;
            }

            // Initialize biome tinting system
            _biomeTintManager = new BiomeTintManager(
                _settings.Rendering.BiomeMapSize,
                Lithforge.Voxel.Chunk.ChunkConstants.Size,
                _settings.Rendering.GrassColormap,
                _settings.Rendering.FoliageColormap,
                biomeWaterColors);

            // Set sea level for altitude-based tint adjustment in shader
            Shader.SetGlobalFloat("_SeaLevel", _settings.WorldGen.SeaLevel);
        }

        private void InitializeGameLoop(LoadingScreen loadingScreen, UnityEngine.UIElements.PanelSettings panelSettings)
        {
            CameraController cameraControllerRef = null;
            SettingsScreen settingsScreenRef = null;
            Material armBaseMaterialRef = null;
            Material armOverlayMaterialRef = null;
            Material heldItemMaterialRef = null;

            // Create GameLoop first (needed by PlayerController for spawn readiness)
            _gameLoop = gameObject.AddComponent<GameLoop>();
            _gameLoop.Initialize(
                _chunkManager,
                _generationPipeline,
                _contentResult.NativeStateRegistry,
                _contentResult.NativeAtlasLookup,
                _chunkMeshStore,
                _decorationStage,
                _worldStorage,
                _settings.WorldGen.Seed,
                _settings.Chunk);

            // Wire biome tint manager to generation scheduler
            if (_biomeTintManager != null)
            {
                _gameLoop.SetBiomeTintManager(_biomeTintManager);
            }

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
                mainCamera.nearClipPlane = _settings.Rendering.NearClipPlane;
                mainCamera.farClipPlane = _settings.Rendering.FarClipPlane;

                // Add PlayerController to player object
                PlayerController playerController = playerObject.AddComponent<PlayerController>();
                playerController.Initialize(
                    _chunkManager, _contentResult.NativeStateRegistry,
                    _gameLoop, _settings.Physics);
                // Add CameraController to camera
                CameraController cameraController = mainCamera.gameObject.AddComponent<CameraController>();
                cameraControllerRef = cameraController;
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
                    _contentResult.TagRegistry,
                    playerObject.transform,
                    _settings.Physics,
                    playerController);

                // Initialize first-person arm renderer
                SkinLoader skinLoader = new SkinLoader();
                Texture2D skinTexture = skinLoader.LoadSkin("default.png");

                if (skinTexture == null)
                {
                    skinTexture = skinLoader.CreateDefaultSkin();
                }

                Shader armBaseShader = Shader.Find("Lithforge/PlayerArm");
                Shader armOverlayShader = Shader.Find("Lithforge/PlayerArmOverlay");
                Shader heldItemShader = Shader.Find("Lithforge/HeldItem");

                if (armBaseShader != null && armOverlayShader != null && heldItemShader != null)
                {
                    Material armBaseMaterial = new Material(armBaseShader);
                    Material armOverlayMaterial = new Material(armOverlayShader);
                    Material heldItemMaterial = new Material(heldItemShader);
                    armBaseMaterialRef = armBaseMaterial;
                    armOverlayMaterialRef = armOverlayMaterial;
                    heldItemMaterialRef = heldItemMaterial;

                    // Share atlas texture with held item material
                    heldItemMaterial.SetTexture("_AtlasArray", _contentResult.AtlasResult.TextureArray);

                    // Share sun light factor materials with voxel materials
                    Material opaqueMat = _chunkMeshStore.OpaqueMaterial;

                    if (opaqueMat != null)
                    {
                        float sunFactor = opaqueMat.GetFloat("_SunLightFactor");
                        float ambient = opaqueMat.GetFloat("_AmbientLight");
                        armBaseMaterial.SetFloat("_SunLightFactor", sunFactor);
                        armBaseMaterial.SetFloat("_AmbientLight", ambient);
                        armOverlayMaterial.SetFloat("_SunLightFactor", sunFactor);
                        armOverlayMaterial.SetFloat("_AmbientLight", ambient);
                        heldItemMaterial.SetFloat("_SunLightFactor", sunFactor);
                        heldItemMaterial.SetFloat("_AmbientLight", ambient);
                    }

                    FirstPersonArmRenderer armRenderer = new FirstPersonArmRenderer(
                        armBaseMaterial,
                        armOverlayMaterial,
                        heldItemMaterial,
                        skinTexture,
                        playerObject.transform,
                        playerInventory,
                        _contentResult.ItemRegistry,
                        _contentResult.StateRegistry,
                        _contentResult.DisplayTransformLookup);

                    _gameLoop.SetArmRenderer(armRenderer, playerController, blockInteraction);
                }
                else
                {
                    UnityEngine.Debug.LogWarning(
                        "[Lithforge] Arm shaders not found. First-person arms will not render.");
                }

                // Set NativeStateRegistry on ChunkManager for block entity flag checks
                _chunkManager.SetNativeStateRegistry(_contentResult.NativeStateRegistry);

                // Initialize block entity tick scheduler
                BlockEntityTickScheduler blockEntityTickScheduler = new BlockEntityTickScheduler(
                    _chunkManager,
                    _contentResult.BlockEntityRegistry,
                    _contentResult.StateRegistry);

                _gameLoop.SetBlockEntityTickScheduler(blockEntityTickScheduler);

                // Wire block entity registry to generation scheduler for deserialization
                _gameLoop.SetBlockEntityRegistry(_contentResult.BlockEntityRegistry);

                // PanelSettings already loaded by Start() coroutine
                if (panelSettings == null)
                {
                    UnityEngine.Debug.LogError(
                        "[Lithforge] DefaultPanelSettings not found in Resources/. UI will not display.");
                }

                // Add CrosshairHUD
                GameObject crosshairObject = new GameObject("CrosshairHUD");
                CrosshairHUD crosshairHUD = crosshairObject.AddComponent<CrosshairHUD>();
                crosshairHUD.Initialize(panelSettings);

                // Add HotbarDisplay
                GameObject hotbarObject = new GameObject("HotbarDisplay");
                HotbarDisplay hotbarDisplay = hotbarObject.AddComponent<HotbarDisplay>();
                hotbarDisplay.Initialize(
                    playerInventory, panelSettings,
                    _contentResult.ItemRegistry,
                    _contentResult.ItemSpriteAtlas);

                // Add PlayerInventoryScreen
                GameObject inventoryObject = new GameObject("PlayerInventoryScreen");
                PlayerInventoryScreen inventoryScreen = inventoryObject.AddComponent<PlayerInventoryScreen>();
                inventoryScreen.Initialize(
                    playerInventory,
                    _contentResult.ItemRegistry,
                    _contentResult.CraftingEngine,
                    panelSettings,
                    _contentResult.ItemSpriteAtlas);

                // Add ChestScreen
                GameObject chestScreenObject = new GameObject("ChestScreen");
                ChestScreen chestScreen = chestScreenObject.AddComponent<ChestScreen>();
                chestScreen.Initialize(
                    playerInventory,
                    _contentResult.ItemRegistry,
                    panelSettings,
                    _contentResult.ItemSpriteAtlas);

                // Add FurnaceScreen
                GameObject furnaceScreenObject = new GameObject("FurnaceScreen");
                FurnaceScreen furnaceScreen = furnaceScreenObject.AddComponent<FurnaceScreen>();
                furnaceScreen.Initialize(
                    playerInventory,
                    _contentResult.ItemRegistry,
                    panelSettings,
                    _contentResult.ItemSpriteAtlas);

                // Wire block entity screens to BlockInteraction
                blockInteraction.SetBlockEntityReferences(
                    blockEntityTickScheduler, chestScreen, furnaceScreen);
                // Add SettingsScreen (initialized after TimeOfDayController is created below)
                GameObject settingsObject = new GameObject("SettingsScreen");
                SettingsScreen settingsScreen = settingsObject.AddComponent<SettingsScreen>();
                settingsScreenRef = settingsScreen;

                // Add debug HUD
                DebugOverlayHUD debugHud = gameObject.AddComponent<DebugOverlayHUD>();
                debugHud.Initialize(
                    _gameLoop, _chunkManager, _settings.Debug, _chunkMeshStore,
                    playerController, _chunkPool);

                // Add benchmark runner
                BenchmarkRunner benchmarkRunner = gameObject.AddComponent<BenchmarkRunner>();
                benchmarkRunner.Initialize(
                    playerController,
                    playerObject.transform,
                    _gameLoop,
                    _settings.Debug.BenchmarkFlySpeed,
                    _settings.Debug.BenchmarkDuration);

                // Hide all gameplay HUD until spawn is complete
                HudVisibilityController hudVisibility = new HudVisibilityController(
                    crosshairHUD, hotbarDisplay, inventoryScreen, debugHud, settingsScreen);
                hudVisibility.HideAll();

                // Create SpawnManager to coordinate chunk loading and player placement
                UnityEngine.Debug.Assert(
                    _settings.Chunk.SpawnLoadRadius <= _settings.Chunk.LOD1Distance,
                    $"[Lithforge] Spawn radius ({_settings.Chunk.SpawnLoadRadius}) exceeds LOD1 distance ({_settings.Chunk.LOD1Distance}). " +
                    "Spawn area chunks would be LOD-meshed, delaying spawn completion.");

                SpawnManager spawnManager = new SpawnManager(
                    _chunkManager,
                    _contentResult.NativeStateRegistry,
                    playerObject.transform,
                    _settings.Chunk.SpawnLoadRadius,
                    _settings.Chunk.YLoadMin,
                    _settings.Chunk.YLoadMax,
                    _settings.Chunk.SpawnFallbackY);
                _gameLoop.SetSpawnManager(spawnManager);

                // Connect the existing loading screen to the spawn manager
                loadingScreen.SetSpawnManager(spawnManager, () => { hudVisibility.ShowGameplay(); });
            }

            // Initialize day/night cycle
            Material material = _chunkMeshStore.OpaqueMaterial;

            if (material != null)
            {
                _timeOfDayController = gameObject.AddComponent<TimeOfDayController>();
                _timeOfDayController.Initialize(
                    material,
                    _chunkMeshStore.CutoutMaterial,
                    _chunkMeshStore.TranslucentMaterial,
                    _settings.Rendering);

                // Register arm materials for day/night cycle updates
                _timeOfDayController.RegisterMaterial(armBaseMaterialRef);
                _timeOfDayController.RegisterMaterial(armOverlayMaterialRef);
                _timeOfDayController.RegisterMaterial(heldItemMaterialRef);

                // Initialize procedural sky
                _skyController = gameObject.AddComponent<SkyController>();
                _skyController.Initialize(
                    _timeOfDayController,
                    _timeOfDayController.DirectionalLight,
                    _settings.Rendering,
                    _chunkManager);
            }

            // Initialize SettingsScreen now that all systems are available
            if (settingsScreenRef != null)
            {
                settingsScreenRef.Initialize(
                    _chunkManager,
                    cameraControllerRef,
                    _timeOfDayController,
                    _chunkMeshStore,
                    panelSettings);
            }
        }

        private static byte BuildSurfaceFlags(BiomeDefinition def)
        {
            byte flags = 0;
            if (def.IsOcean)
            {
                flags |= NativeBiomeSurfaceFlags.IsOcean;
            }
            if (def.IsFrozen)
            {
                flags |= NativeBiomeSurfaceFlags.IsFrozen;
            }
            if (def.IsBeach)
            {
                flags |= NativeBiomeSurfaceFlags.IsBeach;
            }
            return flags;
        }

        private static uint PackColor(Color c)
        {
            byte r = (byte)(Mathf.Clamp01(c.r) * 255f);
            byte g = (byte)(Mathf.Clamp01(c.g) * 255f);
            byte b = (byte)(Mathf.Clamp01(c.b) * 255f);
            byte a = (byte)(Mathf.Clamp01(c.a) * 255f);
            return ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | a;
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

            if (_biomeTintManager != null)
            {
                _biomeTintManager.Dispose();
            }

            if (_chunkMeshStore != null)
            {
                _chunkMeshStore.Dispose();
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
