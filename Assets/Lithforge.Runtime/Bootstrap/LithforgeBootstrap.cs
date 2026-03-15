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
using Lithforge.Runtime.Debug.Benchmark;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Player;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.Spawn;
using Lithforge.Runtime.UI;
using Lithforge.Runtime.UI.Screens;
using Lithforge.Runtime.World;
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
    /// <summary>
    /// Top-level MonoBehaviour that orchestrates the entire game lifecycle.
    /// On Start it loops between the world-selection screen and game sessions:
    /// each session runs ContentPipeline, creates the chunk system, schedulers,
    /// rendering, UI, and player, then yields until a quit-to-title request
    /// triggers save, dispose, and a return to world selection.
    /// </summary>
    /// <remarks>
    /// This component lives on a single root GameObject that persists for the
    /// application lifetime. Per-session systems are attached as child components
    /// or separate GameObjects and torn down by <c>ShutdownSessionResources</c>.
    /// Native containers allocated here (biome data, ore configs, chunk pool) are
    /// disposed in a strict order to avoid double-free or use-after-free.
    /// </remarks>
    public sealed class LithforgeBootstrap : MonoBehaviour
    {
        /// <summary>Optional inspector-assigned opaque voxel material. Falls back to Shader.Find at runtime.</summary>
        [FormerlySerializedAs("_voxelMaterial"),SerializeField] private Material voxelMaterial;
        /// <summary>Optional inspector-assigned compute shader for GPU frustum culling. Falls back to Resources.Load.</summary>
        [FormerlySerializedAs("_frustumCullShader"),SerializeField] private ComputeShader frustumCullShader;
        /// <summary>Optional inspector-assigned compute shader for Hi-Z mipmap generation. Falls back to Resources.Load.</summary>
        [FormerlySerializedAs("_hiZGenerateShader"),SerializeField] private ComputeShader hiZGenerateShader;

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
        private WorldMetadata _worldMetadata;
        private SessionLockHandle _sessionLockHandle;
        private AutoSaveManager _autoSaveManager;
        private Lithforge.Core.Logging.ILogger _logger;
        private TimeOfDayController _timeOfDayController;
        private SkyController _skyController;
        private BiomeTintManager _biomeTintManager;
        private AsyncChunkSaver _asyncChunkSaver;
        private PauseMenuScreen _pauseMenuScreen;
        private bool _quitToTitle;
        private bool _quitInProgress;
        private bool _sessionShutdownComplete;

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
            UnityEngine.UIElements.PanelSettings earlyPanelSettings =
                Resources.Load<UnityEngine.UIElements.PanelSettings>("DefaultPanelSettings");

            while (true)
            {
                // Show world selection screen and wait for user to choose a world
                GameObject selectionObject = new GameObject("WorldSelectionScreen");
                WorldSelectionScreen selectionScreen = selectionObject.AddComponent<WorldSelectionScreen>();
                selectionScreen.Initialize(earlyPanelSettings);

                while (WorldLauncher.SelectedWorldPath == null)
                {
                    yield return null;
                }

                // WorldSelectionScreen destroys itself on selection, but ensure cleanup
                if (selectionObject != null)
                {
                    Destroy(selectionObject);
                }

                // Run one game session — returns when _quitToTitle is set
                yield return StartCoroutine(RunGameSession(earlyPanelSettings));

                // Session ended (quit to title), loop back to world selection
            }
        }

        private IEnumerator RunGameSession(UnityEngine.UIElements.PanelSettings panelSettings)
        {
            _quitToTitle = false;
            _quitInProgress = false;
            _sessionShutdownComplete = false;

            // Create loading screen for content pipeline and spawn
            GameObject loadingObject = new GameObject("LoadingScreen");
            LoadingScreen loadingScreen = loadingObject.AddComponent<LoadingScreen>();
            loadingScreen.Initialize(null, panelSettings, null);

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
            InitializeGameLoop(loadingScreen, panelSettings);

            UnityEngine.Debug.Log("[Lithforge] Bootstrap complete.");

            // Wait until quit-to-title is requested
            while (!_quitToTitle)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Coroutine triggered by PauseMenuScreen "Save and Quit to Title".
        /// Saves player state, flushes chunks, disposes session resources,
        /// and signals RunGameSession to return (back to world selection).
        /// </summary>
        private IEnumerator QuitToTitleCoroutine()
        {
            if (_quitInProgress)
            {
                yield break;
            }

            _quitInProgress = true;

            // Freeze the game immediately
            if (_gameLoop != null)
            {
                _gameLoop.SetGameState(GameState.PausedFull);
            }

            // Hide pause menu without locking cursor (world selection needs cursor free)
            if (_pauseMenuScreen != null)
            {
                _pauseMenuScreen.HideOverlay();
            }

            // Let the final frame render
            yield return null;

            // Shut down session resources (save, dispose, cleanup)
            ShutdownSessionResources();

            // Signal RunGameSession to return
            _quitToTitle = true;
        }

        /// <summary>
        /// Saves all game state, completes in-flight jobs, disposes native resources,
        /// and destroys all session GameObjects. Called by both QuitToTitleCoroutine
        /// and OnDestroy. Guarded by _sessionShutdownComplete to prevent double-dispose.
        /// </summary>
        private void ShutdownSessionResources()
        {
            if (_sessionShutdownComplete)
            {
                return;
            }

            _sessionShutdownComplete = true;

            // Complete all in-flight jobs before saving
            try
            {
                if (_gameLoop != null)
                {
                    _gameLoop.Shutdown();
                }

                // Save player state, serialize all chunks, and flush
                if (_autoSaveManager != null)
                {
                    _autoSaveManager.SaveMetadataOnly();
                }

                if (_asyncChunkSaver != null)
                {
                    _asyncChunkSaver.Flush();
                }

                if (_worldStorage != null && _chunkManager != null)
                {
                    _chunkManager.SaveAllChunks(_worldStorage);
                    _worldStorage.FlushAll();
                }

                if (_asyncChunkSaver != null)
                {
                    _asyncChunkSaver.Dispose();
                    _asyncChunkSaver = null;
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[Lithforge] Error during shutdown save: {ex}");
            }

            // Dispose native resources
            try
            {
                if (_biomeTintManager != null)
                {
                    _biomeTintManager.Dispose();
                    _biomeTintManager = null;
                }

                if (_chunkMeshStore != null)
                {
                    _chunkMeshStore.Dispose();
                    _chunkMeshStore = null;
                }

                if (_chunkManager != null)
                {
                    _chunkManager.Dispose();
                    _chunkManager = null;
                }

                if (_chunkPool != null)
                {
                    _chunkPool.Dispose();
                    _chunkPool = null;
                }

                if (_contentResult != null)
                {
                    if (_contentResult.NativeStateRegistry.States.IsCreated)
                    {
                        _contentResult.NativeStateRegistry.Dispose();
                    }

                    _contentResult.NativeAtlasLookup.Dispose();
                    _contentResult = null;
                }

                if (_nativeBiomeData.IsCreated)
                {
                    _nativeBiomeData.Dispose();
                }

                if (_nativeOreConfigs.IsCreated)
                {
                    _nativeOreConfigs.Dispose();
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[Lithforge] Error during resource disposal: {ex}");
            }

            // Release storage and session lock
            if (_worldStorage != null)
            {
                try
                {
                    _worldStorage.Dispose();
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[Lithforge] Error disposing WorldStorage: {ex}");
                }

                _worldStorage = null;
            }

            if (_sessionLockHandle != null)
            {
                try
                {
                    _sessionLockHandle.Dispose();
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[Lithforge] Error disposing session lock: {ex}");
                }

                _sessionLockHandle = null;
            }

            // Destroy all session GameObjects (children of this bootstrap object are
            // cleaned up by Unity, but separately-rooted GameObjects must be destroyed manually)
            DestroySessionGameObjects();

            // Remove all session components from the bootstrap GameObject
            DestroyComponentIfPresent<GameLoop>();
            DestroyComponentIfPresent<TimeOfDayController>();
            DestroyComponentIfPresent<SkyController>();
            DestroyComponentIfPresent<ChunkBorderRenderer>();
            DestroyComponentIfPresent<F3DebugOverlay>();
            DestroyComponentIfPresent<Debug.Benchmark.BenchmarkRunner>();

            _gameLoop = null;
            _timeOfDayController = null;
            _skyController = null;
            _autoSaveManager = null;
            _decorationStage = null;
            _generationPipeline = null;
            _worldMetadata = null;
            _pauseMenuScreen = null;

            WorldLauncher.Clear();

            UnityEngine.Debug.Log("[Lithforge] Session resources shut down.");
        }

        /// <summary>
        /// Destroys all session GameObjects that were created during InitializeGameLoop.
        /// These are rooted GameObjects (not children of the bootstrap object).
        /// </summary>
        private void DestroySessionGameObjects()
        {
            string[] sessionObjectNames = new string[]
            {
                "Player",
                "BlockHighlight",
                "CrosshairHUD",
                "HotbarDisplay",
                "PlayerInventoryScreen",
                "ContainerScreenManager",
                "SettingsScreen",
                "PauseMenuScreen",
                "ChestScreen",
                "FurnaceScreen",
                "ToolStationScreen",
                "CraftingTableScreen",
            };

            for (int i = 0; i < sessionObjectNames.Length; i++)
            {
                GameObject obj = GameObject.Find(sessionObjectNames[i]);

                if (obj != null)
                {
                    Destroy(obj);
                }
            }
        }

        private void DestroyComponentIfPresent<T>() where T : Component
        {
            T component = GetComponent<T>();

            if (component != null)
            {
                Destroy(component);
            }
        }

        private void InitializeStorage()
        {
            string worldDir = WorldLauncher.SelectedWorldPath;

            // Acquire session lock before opening storage
            if (!SessionLock.TryAcquire(worldDir, out _sessionLockHandle))
            {
                UnityEngine.Debug.LogError(
                    $"[Lithforge] Could not acquire session lock for {worldDir}. World may be open in another instance.");
            }

            _worldStorage = new WorldStorage(worldDir, _logger);

            // Load or create world metadata
            _worldMetadata = _worldStorage.LoadMetadata();

            if (_worldMetadata == null)
            {
                // New world or corrupt metadata — create fresh
                _worldMetadata = new WorldMetadata();
                _worldMetadata.DisplayName = WorldLauncher.SelectedDisplayName ?? "New World";
                _worldMetadata.Seed = WorldLauncher.SelectedSeed;
                _worldMetadata.GameMode = WorldLauncher.SelectedGameMode;
                _worldStorage.SaveMetadataFull(_worldMetadata);
            }

            UnityEngine.Debug.Log(
                $"[Lithforge] World storage: {worldDir} (seed={_worldMetadata.Seed}, mode={_worldMetadata.GameMode})");
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

            _asyncChunkSaver = new AsyncChunkSaver(_worldStorage, _logger);
            _chunkManager.SetAsyncSaver(_asyncChunkSaver);
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
                    Shader fallbackShader = Shader.Find("Universal Render Pipeline/Lit");

                    if (fallbackShader == null)
                    {
                        fallbackShader = Shader.Find("Hidden/InternalErrorShader");
                    }

                    UnityEngine.Debug.LogError("[Lithforge] VoxelOpaque shader not found! Shaders must be in Always Included Shaders or referenced by a scene material. Using fallback.");
                    opaqueMaterial = new Material(fallbackShader);
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
            ComputeShader cullShader = frustumCullShader;

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
            ComputeShader hiZShader = hiZGenerateShader;

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
            Tick.TickRegistry tickRegistryRef = null;
            bool hasRestoredState = false;
            float restoredTimeOfDay = 0f;

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
                _worldMetadata.Seed,
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
                playerController.Initialize();
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

                // Restore player state from metadata, or grant starting items for new worlds
                if (_worldMetadata.PlayerState != null && !WorldLauncher.IsNewWorld)
                {
                    PlayerStateSerializer.Restore(
                        _worldMetadata.PlayerState,
                        playerObject.transform,
                        mainCamera,
                        playerInventory,
                        _contentResult.ItemRegistry,
                        out restoredTimeOfDay);
                    hasRestoredState = true;
                    UnityEngine.Debug.Log("[Lithforge] Player state restored from save.");
                }
                else
                {
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

                Shader armBaseShader = Shader.Find("Lithforge/PlayerModel");
                Shader armOverlayShader = Shader.Find("Lithforge/PlayerModelOverlay");
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

                    PlayerRenderer playerRenderer = new PlayerRenderer(
                        armBaseMaterial,
                        armOverlayMaterial,
                        heldItemMaterial,
                        skinTexture,
                        playerObject.transform,
                        mainCamera.transform,
                        playerInventory,
                        _contentResult.ItemRegistry,
                        _contentResult.StateRegistry,
                        _contentResult.DisplayTransformLookup);

                    _gameLoop.SetPlayerRenderer(playerRenderer, playerController, blockInteraction);
                }
                else
                {
                    UnityEngine.Debug.LogWarning(
                        "[Lithforge] PlayerModel shaders not found. Player model will not render.");
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

                // Build ScreenContext — single shared context for all container screens
                ScreenContext screenContext = new ScreenContext(
                    playerInventory,
                    _contentResult.ItemRegistry,
                    _contentResult.ItemSpriteAtlas,
                    panelSettings,
                    _contentResult.CraftingEngine,
                    _contentResult.ToolTraitRegistry);

                // Add PlayerInventoryScreen (not a block entity screen, managed separately)
                GameObject inventoryObject = new GameObject("PlayerInventoryScreen");
                PlayerInventoryScreen inventoryScreen = inventoryObject.AddComponent<PlayerInventoryScreen>();
                inventoryScreen.Initialize(screenContext);

                // Create ContainerScreenManager (manages all block entity screens lazily)
                GameObject screenManagerObject = new GameObject("ContainerScreenManager");
                ContainerScreenManager screenManager =
                    screenManagerObject.AddComponent<ContainerScreenManager>();

                screenManager.Register(
                    ChestBlockEntity.TypeIdValue,
                    () =>
                    {
                        GameObject obj = new GameObject("ChestScreen");
                        ChestScreen screen = obj.AddComponent<ChestScreen>();
                        screen.Initialize(screenContext);
                        return screen;
                    },
                    (ContainerScreen s, Lithforge.Runtime.BlockEntity.BlockEntity e) =>
                    {
                        ((ChestScreen)s).OpenForEntity(e);
                    });

                screenManager.Register(
                    FurnaceBlockEntity.TypeIdValue,
                    () =>
                    {
                        GameObject obj = new GameObject("FurnaceScreen");
                        FurnaceScreen screen = obj.AddComponent<FurnaceScreen>();
                        screen.Initialize(screenContext);
                        return screen;
                    },
                    (ContainerScreen s, Lithforge.Runtime.BlockEntity.BlockEntity e) =>
                    {
                        ((FurnaceScreen)s).OpenForEntity(e);
                    });

                screenManager.Register(
                    ToolStationBlockEntity.TypeIdValue,
                    () =>
                    {
                        GameObject obj = new GameObject("ToolStationScreen");
                        ToolStationScreen screen = obj.AddComponent<ToolStationScreen>();
                        screen.Initialize(screenContext);
                        return screen;
                    },
                    (ContainerScreen s, Lithforge.Runtime.BlockEntity.BlockEntity e) =>
                    {
                        ((ToolStationScreen)s).OpenForEntity(e);
                    });

                screenManager.Register(
                    CraftingTableBlockEntity.TypeIdValue,
                    () =>
                    {
                        GameObject obj = new GameObject("CraftingTableScreen");
                        CraftingTableScreen screen = obj.AddComponent<CraftingTableScreen>();
                        screen.Initialize(screenContext);
                        return screen;
                    },
                    (ContainerScreen s, Lithforge.Runtime.BlockEntity.BlockEntity e) =>
                    {
                        ((CraftingTableScreen)s).OpenForEntity(e);
                    });

                // Wire screen manager to BlockInteraction (replaces 6-arg SetBlockEntityReferences)
                blockInteraction.SetBlockEntityReferences(
                    blockEntityTickScheduler, screenManager, _contentResult.ToolTraitRegistry);
                // Add SettingsScreen (initialized after TimeOfDayController is created below)
                GameObject settingsObject = new GameObject("SettingsScreen");
                SettingsScreen settingsScreen = settingsObject.AddComponent<SettingsScreen>();
                settingsScreenRef = settingsScreen;

                // Create MetricsRegistry — shared data source for overlay and benchmarks
                MetricsRegistry metricsRegistry = new MetricsRegistry();
                metricsRegistry.Initialize(
                    _chunkManager,
                    _chunkMeshStore,
                    _chunkPool,
                    playerController,
                    mainCamera,
                    _gameLoop,
                    _settings.Debug.FpsAlpha);
                _gameLoop.SetMetricsRegistry(metricsRegistry);

                // Add chunk border renderer (F3+G toggle)
                ChunkBorderRenderer chunkBorderRenderer = gameObject.AddComponent<ChunkBorderRenderer>();
                chunkBorderRenderer.Initialize(
                    metricsRegistry,
                    mainCamera,
                    _settings.Debug.ChunkBorderRadius);
                chunkBorderRenderer.SetVisible(false);

                // Add F3 debug overlay (replaces old IMGUI DebugOverlayHUD)
                F3DebugOverlay debugOverlay = gameObject.AddComponent<F3DebugOverlay>();
                debugOverlay.Initialize(
                    metricsRegistry,
                    chunkBorderRenderer,
                    _settings.Debug,
                    panelSettings);

                // Add benchmark runner (F5 trigger, SO-driven scenarios)
                BenchmarkContext benchmarkContext = new BenchmarkContext();
                benchmarkContext.Metrics = metricsRegistry;
                benchmarkContext.ChunkManager = _chunkManager;
                benchmarkContext.PlayerController = playerController;
                benchmarkContext.PlayerTransform = playerObject.transform;
                benchmarkContext.MainCamera = mainCamera;
                benchmarkContext.GameLoop = _gameLoop;
                benchmarkContext.BlockInteraction = blockInteraction;

                BenchmarkRunner benchmarkRunner = gameObject.AddComponent<BenchmarkRunner>();
                benchmarkRunner.Initialize(
                    benchmarkContext,
                    _settings.Debug,
                    metricsRegistry,
                    playerController,
                    panelSettings);

                // Create PauseMenuScreen (initialized later after SettingsScreen)
                GameObject pauseMenuObject = new GameObject("PauseMenuScreen");
                PauseMenuScreen pauseMenuScreen = pauseMenuObject.AddComponent<PauseMenuScreen>();
                _pauseMenuScreen = pauseMenuScreen;

                // Hide all gameplay HUD until spawn is complete
                HudVisibilityController hudVisibility = new HudVisibilityController(
                    crosshairHUD, hotbarDisplay, inventoryScreen, debugOverlay,
                    settingsScreen, pauseMenuScreen, screenManager);
                hudVisibility.HideAll();

                // Create SpawnManager to coordinate chunk loading and player placement
                int lod1Dist = SchedulingConfig.LOD1Distance(_settings.Chunk.RenderDistance);
                UnityEngine.Debug.Assert(
                    _settings.Chunk.SpawnLoadRadius <= lod1Dist,
                    $"[Lithforge] Spawn radius ({_settings.Chunk.SpawnLoadRadius}) exceeds LOD1 distance ({lod1Dist}). " +
                    "Spawn area chunks would be LOD-meshed, delaying spawn completion.");

                SpawnManager spawnManager = new SpawnManager(
                    _chunkManager,
                    _contentResult.NativeStateRegistry,
                    playerObject.transform,
                    _settings.Chunk.SpawnLoadRadius,
                    _settings.Chunk.YLoadMin,
                    _settings.Chunk.YLoadMax,
                    _settings.Chunk.SpawnFallbackY);
                if (hasRestoredState)
                {
                    spawnManager.SetSavedPosition();
                }

                _gameLoop.SetSpawnManager(spawnManager);

                // Connect the existing loading screen to the spawn manager
                loadingScreen.SetSpawnManager(spawnManager, () => { hudVisibility.ShowGameplay(); });

                // Create auto-save manager — saves metadata every 30s and flushes dirty chunks every 60s
                _autoSaveManager = new AutoSaveManager(
                    _worldStorage,
                    _worldMetadata,
                    playerObject.transform,
                    mainCamera,
                    () => { return _timeOfDayController != null ? _timeOfDayController.TimeOfDay : 0f; },
                    playerInventory);
                _autoSaveManager.SetAsyncSaver(_asyncChunkSaver);
                _gameLoop.SetAutoSaveManager(_autoSaveManager);

                // Initialize fixed tick rate system (30 TPS)
                Unity.Mathematics.float3 startPos = new Unity.Mathematics.float3(
                    playerObject.transform.position.x,
                    playerObject.transform.position.y,
                    playerObject.transform.position.z);

                Tick.PlayerPhysicsBody physicsBody = new Tick.PlayerPhysicsBody(
                    startPos,
                    playerObject.transform,
                    _chunkManager,
                    _contentResult.NativeStateRegistry,
                    _settings.Physics);

                playerController.SetPhysicsBody(physicsBody);

                Tick.PlayerInputLatch inputLatch = new Tick.PlayerInputLatch();

                tickRegistryRef = new Tick.TickRegistry();
                tickRegistryRef.Register(new Tick.MiningTickAdapter(blockInteraction));
                tickRegistryRef.Register(new Tick.BlockEntityTickAdapter(blockEntityTickScheduler));

                _gameLoop.SetTickSystems(
                    tickRegistryRef, inputLatch, physicsBody, playerObject.transform);
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

                // Restore time of day from saved state
                if (hasRestoredState)
                {
                    _timeOfDayController.SetTimeOfDay(restoredTimeOfDay);
                }

                // Register arm materials for day/night cycle updates
                _timeOfDayController.RegisterMaterial(armBaseMaterialRef);
                _timeOfDayController.RegisterMaterial(armOverlayMaterialRef);
                _timeOfDayController.RegisterMaterial(heldItemMaterialRef);

                // Register time-of-day adapter in the fixed tick loop
                if (tickRegistryRef != null)
                {
                    tickRegistryRef.Register(new Tick.TimeOfDayTickAdapter(_timeOfDayController));
                }

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
                    panelSettings,
                    _gameLoop.NotifyRenderDistanceChanged);
            }

            // Initialize PauseMenuScreen with callbacks
            if (_pauseMenuScreen != null && settingsScreenRef != null)
            {
                SettingsScreen settingsCapture = settingsScreenRef;

                _pauseMenuScreen.Initialize(
                    panelSettings,
                    settingsCapture,
                    // onPause: freeze game and show pause menu
                    () =>
                    {
                        _gameLoop.SetGameState(GameState.PausedFull);
                        _pauseMenuScreen.Open();
                    },
                    // onResume: unfreeze game and close pause menu
                    () =>
                    {
                        _pauseMenuScreen.Close();
                        _gameLoop.SetGameState(GameState.Playing);
                    },
                    // onOptions: hide pause overlay, open settings from pause
                    () =>
                    {
                        _pauseMenuScreen.HideOverlay();
                        settingsCapture.SetOnCloseCallback(() =>
                        {
                            _pauseMenuScreen.Open();
                        });
                        settingsCapture.Open();
                        // Mark as opened from pause so Close button returns to pause
                        settingsCapture.OpenedFromPause = true;
                    },
                    // onQuitToTitle: save and return to world selection
                    () =>
                    {
                        StartCoroutine(QuitToTitleCoroutine());
                    });
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
            ShutdownSessionResources();
        }
    }

}
