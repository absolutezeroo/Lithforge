using System;
using System.Collections;
using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Core.Validation;
using Lithforge.Runtime.Audio;
using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.BlockEntity.UI;
using Lithforge.Runtime.Content.Blocks;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Content.Tools;
using Lithforge.Runtime.Content.WorldGen;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Debug.Benchmark;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Player;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Scheduling;
using Lithforge.Runtime.Spawn;
using Lithforge.Runtime.Tick;
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
using Lithforge.WorldGen.River;

using Unity.Collections;
using Unity.Mathematics;

using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

using AudioSettings = Lithforge.Runtime.Content.Settings.AudioSettings;
using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Bootstrap
{
    /// <summary>
    ///     Top-level MonoBehaviour that orchestrates the entire game lifecycle.
    ///     On Start it loops between the world-selection screen and game sessions:
    ///     each session runs ContentPipeline, creates the chunk system, schedulers,
    ///     rendering, UI, and player, then yields until a quit-to-title request
    ///     triggers save, dispose, and a return to world selection.
    /// </summary>
    /// <remarks>
    ///     This component lives on a single root GameObject that persists for the
    ///     application lifetime. Per-session systems are attached as child components
    ///     or separate GameObjects and torn down by <c>ShutdownSessionResources</c>.
    ///     Native containers allocated here (biome data, ore configs, chunk pool) are
    ///     disposed in a strict order to avoid double-free or use-after-free.
    /// </remarks>
    public sealed class LithforgeBootstrap : MonoBehaviour
    {
        /// <summary>Optional inspector-assigned opaque voxel material. Falls back to Shader.Find at runtime.</summary>
        [FormerlySerializedAs("_voxelMaterial"), SerializeField]
        private Material voxelMaterial;
        /// <summary>Optional inspector-assigned compute shader for GPU frustum culling. Falls back to Resources.Load.</summary>
        [FormerlySerializedAs("_frustumCullShader"), SerializeField]
        private ComputeShader frustumCullShader;
        /// <summary>Optional inspector-assigned compute shader for Hi-Z mipmap generation. Falls back to Resources.Load.</summary>
        [FormerlySerializedAs("_hiZGenerateShader"), SerializeField]
        private ComputeShader hiZGenerateShader;
        private AsyncChunkSaver _asyncChunkSaver;
        private AutoSaveManager _autoSaveManager;
        private BiomeAmbientPlayer _biomeAmbientPlayer;
        private BiomeTintManager _biomeTintManager;
        private ChunkManager _chunkManager;
        private ChunkMeshStore _chunkMeshStore;
        private ChunkPool _chunkPool;
        private ContentPipelineResult _contentResult;
        private DecorationStage _decorationStage;
        private GameLoop _gameLoop;
        private GenerationPipeline _generationPipeline;
        private ILogger _logger;
        private NativeArray<NativeBiomeData> _nativeBiomeData;
        private NativeArray<NativeOreConfig> _nativeOreConfigs;
        private PauseMenuScreen _pauseMenuScreen;
        private bool _quitInProgress;
        private bool _quitToTitle;
        private SessionLockHandle _sessionLockHandle;
        private bool _sessionShutdownComplete;

        private LoadedSettings _settings;
        private SfxSourcePool _sfxSourcePool;
        private SkyController _skyController;
        private TimeOfDayController _timeOfDayController;
        private UserPreferences _userPreferences;
        private WorldMetadata _worldMetadata;
        private WorldStorage _worldStorage;

        private void Awake()
        {
            _settings = SettingsLoader.Load();
            _logger = new UnityLogger();
            _userPreferences = UserPreferences.Load();

            // Initialize profiling systems
            FrameProfiler.Init();
            FrameProfiler.Enabled = _settings.Debug.EnableProfiling;
            PipelineStats.Enabled = _settings.Debug.EnableProfiling;
        }

        private IEnumerator Start()
        {
            PanelSettings earlyPanelSettings =
                Resources.Load<PanelSettings>("DefaultPanelSettings");

            while (true)
            {
                // Show world selection screen and wait for user to choose a world
                GameObject selectionObject = new("WorldSelectionScreen");
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

        private void OnDestroy()
        {
            ShutdownSessionResources();
        }

        private IEnumerator RunGameSession(PanelSettings panelSettings)
        {
            _quitToTitle = false;
            _quitInProgress = false;
            _sessionShutdownComplete = false;

            // Create loading screen for content pipeline and spawn
            GameObject loadingObject = new("LoadingScreen");
            LoadingScreen loadingScreen = loadingObject.AddComponent<LoadingScreen>();
            loadingScreen.Initialize(null, panelSettings, null);

            // Run content pipeline as iterator, yielding frames between phases
            ContentValidator validator = new();
            ContentPipeline pipeline = new(
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
        ///     Coroutine triggered by PauseMenuScreen "Save and Quit to Title".
        ///     Saves player state, flushes chunks, disposes session resources,
        ///     and signals RunGameSession to return (back to world selection).
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

            // Create saving screen overlay
            PanelSettings panelSettings =
                Resources.Load<PanelSettings>("DefaultPanelSettings");
            GameObject savingObject = new("SavingScreen");
            SavingScreen savingScreen = savingObject.AddComponent<SavingScreen>();
            savingScreen.Initialize(panelSettings);

            SaveProgress progress = new();
            progress.Phase = SaveState.CompletingJobs;
            savingScreen.SetProgress(progress);

            // Let the saving screen render
            yield return null;

            // Phase 1: Complete in-flight jobs
            try
            {
                if (_gameLoop != null)
                {
                    _gameLoop.Shutdown();
                }

                if (_autoSaveManager != null)
                {
                    _autoSaveManager.SaveMetadataOnly();
                }

                if (_asyncChunkSaver != null)
                {
                    _asyncChunkSaver.Flush();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Lithforge] Error completing jobs during save: {ex}");
            }

            yield return null;

            // Phase 2: Save dirty chunks incrementally
            progress.Phase = SaveState.SavingChunks;

            if (_worldStorage != null && _chunkManager != null)
            {
                List<ManagedChunk> dirtyChunks = new();
                _chunkManager.CollectDirtyChunks(dirtyChunks);
                progress.TotalChunks = dirtyChunks.Count;
                progress.SavedChunks = 0;
                savingScreen.SetProgress(progress);

                for (int i = 0; i < dirtyChunks.Count; i++)
                {
                    ManagedChunk chunk = dirtyChunks[i];

                    try
                    {
                        chunk.ActiveJobHandle.Complete();
                        _worldStorage.SaveChunk(
                            chunk.Coord, chunk.Data, chunk.LightData, chunk.BlockEntities);
                        chunk.IsDirty = false;
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError(
                            $"[Lithforge] Error saving chunk {chunk.Coord}: {ex}");
                    }

                    progress.SavedChunks = i + 1;
                    savingScreen.SetProgress(progress);

                    // Yield every 8 chunks to keep the UI responsive
                    if ((i + 1) % 8 == 0)
                    {
                        yield return null;
                    }
                }

                // Phase 3: Flush dirty regions incrementally
                progress.Phase = SaveState.FlushingRegions;

                List<RegionFile> dirtyRegions = new();
                _worldStorage.CollectDirtyRegions(dirtyRegions);
                progress.TotalRegions = dirtyRegions.Count;
                progress.FlushedRegions = 0;
                savingScreen.SetProgress(progress);

                for (int i = 0; i < dirtyRegions.Count; i++)
                {
                    _worldStorage.FlushRegion(dirtyRegions[i]);
                    progress.FlushedRegions = i + 1;
                    savingScreen.SetProgress(progress);
                    yield return null;
                }
            }

            // Dispose async saver after all chunks are saved
            if (_asyncChunkSaver != null)
            {
                _asyncChunkSaver.Dispose();
                _asyncChunkSaver = null;
            }

            progress.Phase = SaveState.Done;
            savingScreen.SetProgress(progress);

            // Destroy saving screen
            Destroy(savingObject);

            // Dispose all remaining session resources
            DisposeSessionResources();

            // Signal RunGameSession to return
            _quitToTitle = true;
        }

        /// <summary>
        ///     Synchronous save + dispose fallback for OnDestroy (application quit).
        ///     Performs both saving and disposal in one call. Guarded by
        ///     _sessionShutdownComplete to prevent double-dispose.
        /// </summary>
        private void ShutdownSessionResources()
        {
            if (_sessionShutdownComplete)
            {
                return;
            }

            // Save synchronously (fallback path — no coroutine possible)
            SaveSessionResourcesSync();

            // Dispose everything
            DisposeSessionResources();
        }

        /// <summary>
        ///     Synchronous save of all game state: metadata, chunks, regions.
        ///     Used by the OnDestroy fallback path.
        /// </summary>
        private void SaveSessionResourcesSync()
        {
            try
            {
                if (_gameLoop != null)
                {
                    _gameLoop.Shutdown();
                }

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
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Lithforge] Error during shutdown save: {ex}");
            }
        }

        /// <summary>
        ///     Disposes native resources, destroys session GameObjects, and releases
        ///     storage and session lock. Called after save completes (either incremental
        ///     coroutine or synchronous fallback).
        /// </summary>
        private void DisposeSessionResources()
        {
            if (_sessionShutdownComplete)
            {
                return;
            }

            _sessionShutdownComplete = true;

            // Dispose audio resources (before native resources)
            try
            {
                if (_sfxSourcePool != null)
                {
                    _sfxSourcePool.Dispose();
                    _sfxSourcePool = null;
                }

                if (_biomeAmbientPlayer != null)
                {
                    _biomeAmbientPlayer.Dispose();
                    _biomeAmbientPlayer = null;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Lithforge] Error disposing audio: {ex}");
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
                    ToolTemplateRegistry.Clear();
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
            catch (Exception ex)
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
                catch (Exception ex)
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
                catch (Exception ex)
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
            DestroyComponentIfPresent<BenchmarkRunner>();

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
        ///     Destroys all session GameObjects that were created during InitializeGameLoop.
        ///     These are rooted GameObjects (not children of the bootstrap object).
        /// </summary>
        private void DestroySessionGameObjects()
        {
            string[] sessionObjectNames =
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
                "SavingScreen",
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
                biomes.Length, Allocator.Persistent);

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
                ores.Length, Allocator.Persistent);

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
            NativeRiverConfig riverConfig = wg.RiverNoise.ToNativeConfig();

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
                ChunkConstants.Size,
                _settings.Rendering.GrassColormap,
                _settings.Rendering.FoliageColormap,
                biomeWaterColors);

            // Set sea level for altitude-based tint adjustment in shader
            Shader.SetGlobalFloat("_SeaLevel", _settings.WorldGen.SeaLevel);
        }

        private void InitializeGameLoop(LoadingScreen loadingScreen, PanelSettings panelSettings)
        {
            CameraController cameraControllerRef = null;
            SettingsScreen settingsScreenRef = null;
            Material armBaseMaterialRef = null;
            Material armOverlayMaterialRef = null;
            Material heldItemMaterialRef = null;
            TickRegistry tickRegistryRef = null;
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
                GameObject playerObject = new("Player");
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
                GameObject highlightObject = new("BlockHighlight");
                BlockHighlight blockHighlight = highlightObject.AddComponent<BlockHighlight>();
                blockHighlight.Initialize(_settings.Rendering);

                // Create player inventory and loot resolver
                Inventory playerInventory = new();
                LootResolver lootResolver = new(_contentResult.LootTables);

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
                        ResourceId itemId = new(entry.itemNamespace, entry.itemName);
                        ItemEntry itemDef = _contentResult.ItemRegistry.Get(itemId);
                        int maxStack = itemDef != null
                            ? itemDef.MaxStackSize
                            : _settings.Physics.DefaultMaxStackSize;

                        byte[] toolData = ToolTemplateRegistry.GetTemplate(itemId);

                        if (toolData != null)
                        {
                            ToolInstance toolTemplate =
                                ToolInstanceSerializer.Deserialize(toolData);
                            int durability = toolTemplate != null
                                ? toolTemplate.MaxDurability : -1;
                            ItemStack toolStack = new(itemId, 1, durability);
                            toolStack.CustomData = toolData;
                            playerInventory.AddItemStack(toolStack);
                        }
                        else
                        {
                            playerInventory.AddItem(itemId, entry.count, maxStack);
                        }
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
                SkinLoader skinLoader = new();
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
                    Material armBaseMaterial = new(armBaseShader);
                    Material armOverlayMaterial = new(armOverlayShader);
                    Material heldItemMaterial = new(heldItemShader);
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

                    PlayerRenderer playerRenderer = new(
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
                BlockEntityTickScheduler blockEntityTickScheduler = new(
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
                GameObject crosshairObject = new("CrosshairHUD");
                CrosshairHUD crosshairHUD = crosshairObject.AddComponent<CrosshairHUD>();
                crosshairHUD.Initialize(panelSettings);

                // Add HotbarDisplay
                GameObject hotbarObject = new("HotbarDisplay");
                HotbarDisplay hotbarDisplay = hotbarObject.AddComponent<HotbarDisplay>();
                hotbarDisplay.Initialize(
                    playerInventory, panelSettings,
                    _contentResult.ItemRegistry,
                    _contentResult.ItemSpriteAtlas);

                // Build ScreenContext — single shared context for all container screens
                ScreenContext screenContext = new(
                    playerInventory,
                    _contentResult.ItemRegistry,
                    _contentResult.ItemSpriteAtlas,
                    panelSettings,
                    _contentResult.CraftingEngine,
                    _contentResult.ToolTraitRegistry,
                    _contentResult.ToolPartTextures,
                    _contentResult.ToolMaterials);

                // Add PlayerInventoryScreen (not a block entity screen, managed separately)
                GameObject inventoryObject = new("PlayerInventoryScreen");
                PlayerInventoryScreen inventoryScreen = inventoryObject.AddComponent<PlayerInventoryScreen>();
                inventoryScreen.Initialize(screenContext);

                // Create ContainerScreenManager (manages all block entity screens lazily)
                GameObject screenManagerObject = new("ContainerScreenManager");
                ContainerScreenManager screenManager =
                    screenManagerObject.AddComponent<ContainerScreenManager>();

                screenManager.Register(
                    ChestBlockEntity.TypeIdValue,
                    () =>
                    {
                        GameObject obj = new("ChestScreen");
                        ChestScreen screen = obj.AddComponent<ChestScreen>();
                        screen.Initialize(screenContext);
                        return screen;
                    },
                    (s, e) =>
                    {
                        ((ChestScreen)s).OpenForEntity(e);
                    });

                screenManager.Register(
                    FurnaceBlockEntity.TypeIdValue,
                    () =>
                    {
                        GameObject obj = new("FurnaceScreen");
                        FurnaceScreen screen = obj.AddComponent<FurnaceScreen>();
                        screen.Initialize(screenContext);
                        return screen;
                    },
                    (s, e) =>
                    {
                        ((FurnaceScreen)s).OpenForEntity(e);
                    });

                screenManager.Register(
                    ToolStationBlockEntity.TypeIdValue,
                    () =>
                    {
                        GameObject obj = new("ToolStationScreen");
                        ToolStationScreen screen = obj.AddComponent<ToolStationScreen>();
                        screen.Initialize(screenContext);
                        return screen;
                    },
                    (s, e) =>
                    {
                        ((ToolStationScreen)s).OpenForEntity(e);
                    });

                screenManager.Register(
                    CraftingTableBlockEntity.TypeIdValue,
                    () =>
                    {
                        GameObject obj = new("CraftingTableScreen");
                        CraftingTableScreen screen = obj.AddComponent<CraftingTableScreen>();
                        screen.Initialize(screenContext);
                        return screen;
                    },
                    (s, e) =>
                    {
                        ((CraftingTableScreen)s).OpenForEntity(e);
                    });

                // Wire screen manager to BlockInteraction (replaces 6-arg SetBlockEntityReferences)
                blockInteraction.SetBlockEntityReferences(
                    blockEntityTickScheduler, screenManager, _contentResult.ToolTraitRegistry);
                // Add SettingsScreen (initialized after TimeOfDayController is created below)
                GameObject settingsObject = new("SettingsScreen");
                SettingsScreen settingsScreen = settingsObject.AddComponent<SettingsScreen>();
                settingsScreenRef = settingsScreen;

                // Create MetricsRegistry — shared data source for overlay and benchmarks
                MetricsRegistry metricsRegistry = new();
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
                BenchmarkContext benchmarkContext = new();
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
                GameObject pauseMenuObject = new("PauseMenuScreen");
                PauseMenuScreen pauseMenuScreen = pauseMenuObject.AddComponent<PauseMenuScreen>();
                _pauseMenuScreen = pauseMenuScreen;

                // Hide all gameplay HUD until spawn is complete
                HudVisibilityController hudVisibility = new(
                    crosshairHUD, hotbarDisplay, inventoryScreen, debugOverlay,
                    settingsScreen, pauseMenuScreen, screenManager);
                hudVisibility.HideAll();

                // Create SpawnManager to coordinate chunk loading and player placement
                // Clamp spawn radius to LOD1 distance so all spawn-area chunks get full-detail meshes
                int lod1Dist = SchedulingConfig.LOD1Distance(_settings.Chunk.RenderDistance);
                int spawnRadius = _settings.Chunk.SpawnLoadRadius;
                if (spawnRadius > lod1Dist)
                {
                    _logger.LogWarning(
                        $"Spawn radius ({spawnRadius}) exceeds LOD1 distance ({lod1Dist}). " +
                        $"Clamping to {lod1Dist} to ensure full-detail meshes in spawn area.");
                    spawnRadius = lod1Dist;
                }

                SpawnManager spawnManager = new(
                    _chunkManager,
                    _contentResult.NativeStateRegistry,
                    playerObject.transform,
                    spawnRadius,
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
                float3 startPos = new(
                    playerObject.transform.position.x,
                    playerObject.transform.position.y,
                    playerObject.transform.position.z);

                PlayerPhysicsBody physicsBody = new(
                    startPos,
                    playerObject.transform,
                    _chunkManager,
                    _contentResult.NativeStateRegistry,
                    _settings.Physics);

                playerController.SetPhysicsBody(physicsBody);

                PlayerInputLatch inputLatch = new();

                tickRegistryRef = new TickRegistry();
                tickRegistryRef.Register(new MiningTickAdapter(blockInteraction));
                tickRegistryRef.Register(new BlockEntityTickAdapter(blockEntityTickScheduler));

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
                    tickRegistryRef.Register(new TimeOfDayTickAdapter(_timeOfDayController));
                }

                // Initialize procedural sky
                _skyController = gameObject.AddComponent<SkyController>();
                _skyController.Initialize(
                    _timeOfDayController,
                    _timeOfDayController.DirectionalLight,
                    _settings.Rendering,
                    _chunkManager);
            }

            // Initialize audio systems
            AudioMixerController audioMixerController = null;
            AudioMixer mixer =
                Resources.Load<AudioMixer>("Audio/LithforgeMixer");

            if (mixer != null)
            {
                audioMixerController = new AudioMixerController(mixer);
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[Lithforge] LithforgeMixer not found at Resources/Audio/. " +
                    "Audio will use AudioListener.volume fallback.");
            }

            AudioSettings audioSettings = _settings.Audio;
            Camera audioCamera = Camera.main;

            if (audioCamera != null && _contentResult.SoundGroupRegistry != null)
            {
                // Create SFX source pool
                AudioMixerGroup sfxGroup =
                    audioMixerController != null ? audioMixerController.GetGroup("SFX") : null;

                GameObject poolHost = new("SfxSourcePool");
                _sfxSourcePool = new SfxSourcePool(
                    poolHost, audioSettings.SfxPoolSize, sfxGroup);

                // Create block sound player
                BlockSoundPlayer blockSoundPlayer = new(
                    _contentResult.SoundGroupRegistry,
                    _contentResult.StateRegistry,
                    _sfxSourcePool,
                    audioSettings.SoundCooldownMs);

                // Wire block sound player to BlockInteraction
                BlockInteraction blockInteractionRef =
                    audioCamera.GetComponent<BlockInteraction>();

                if (blockInteractionRef != null)
                {
                    blockInteractionRef.SetBlockSoundPlayer(
                        blockSoundPlayer, audioSettings.MiningHitInterval);
                }

                // Create footstep controller
                Transform playerTransformForAudio = audioCamera.transform.parent;
                PlayerController pcForAudio =
                    playerTransformForAudio != null
                        ? playerTransformForAudio.GetComponent<PlayerController>()
                        : null;

                FootstepController footstepController = null;

                if (playerTransformForAudio != null && pcForAudio != null)
                {
                    footstepController = new FootstepController(
                        blockSoundPlayer,
                        _chunkManager,
                        _contentResult.StateRegistry,
                        _contentResult.NativeStateRegistry,
                        playerTransformForAudio,
                        audioSettings.FootstepDistance,
                        audioSettings.SprintFootstepDistance,
                        () => { return pcForAudio.OnGround; },
                        () => { return pcForAudio.IsFlying; },
                        () =>
                        {
                            return pcForAudio.PhysicsBody != null &&
                                   pcForAudio.PhysicsBody.IsSprinting;
                        });
                }

                // Create fall sound detector
                FallSoundDetector fallSoundDetector = null;

                if (playerTransformForAudio != null && pcForAudio != null)
                {
                    fallSoundDetector = new FallSoundDetector(
                        blockSoundPlayer,
                        _chunkManager,
                        playerTransformForAudio,
                        audioSettings.FallSoundThreshold,
                        audioSettings.FallMaxVolume,
                        audioSettings.FallMaxHeight,
                        () => { return pcForAudio.OnGround; },
                        () => { return pcForAudio.IsFlying; });
                }

                // Create environmental audio subsystems
                AudioLowPassFilter lowPassFilter =
                    audioCamera.gameObject.AddComponent<AudioLowPassFilter>();
                lowPassFilter.cutoffFrequency = audioSettings.SurfaceCutoff;

                AudioReverbFilter reverbFilter =
                    audioCamera.gameObject.AddComponent<AudioReverbFilter>();
                reverbFilter.reverbPreset = AudioReverbPreset.Off;

                UnderwaterAudioFilter underwaterFilter = new(
                    _chunkManager,
                    _contentResult.NativeStateRegistry,
                    lowPassFilter,
                    audioCamera.transform,
                    audioSettings.UnderwaterCutoff,
                    audioSettings.SurfaceCutoff,
                    audioSettings.UnderwaterLerpSpeed);

                EnclosureProbe enclosureProbe = new(
                    pos =>
                    {
                        StateId sid = _chunkManager.GetBlock(pos);
                        return sid.Value != 0 &&
                               _contentResult.NativeStateRegistry.States.IsCreated &&
                               sid.Value < _contentResult.NativeStateRegistry.States.Length &&
                               (_contentResult.NativeStateRegistry.States[sid.Value].Flags &
                                BlockStateCompact.FlagFullCube) != 0;
                    },
                    audioCamera.transform,
                    audioSettings.EnclosureRayCount,
                    audioSettings.EnclosureMaxDistance,
                    audioSettings.EnclosureUpdateTicks);

                CaveReverbController caveReverb = new(
                    reverbFilter,
                    enclosureProbe,
                    audioSettings.EnclosureReverbThreshold,
                    audioSettings.ReverbLerpSpeed);

                RuntimeBiomeSampler biomeSampler = new(
                    _contentResult.BiomeDefinitions,
                    _worldMetadata.Seed);

                AudioMixerGroup ambientGroup =
                    audioMixerController != null ? audioMixerController.GetGroup("Ambient") : null;

                _biomeAmbientPlayer = new BiomeAmbientPlayer(
                    audioCamera.gameObject,
                    ambientGroup,
                    audioSettings.AmbientCrossfadeTime);

                ScatterSoundPlayer scatterPlayer = new(
                    _sfxSourcePool,
                    playerTransformForAudio,
                    audioSettings.ScatterMinInterval,
                    audioSettings.ScatterMaxInterval,
                    audioSettings.ScatterMinDistance,
                    audioSettings.ScatterMaxDistance,
                    () =>
                    {
                        return _timeOfDayController != null
                            ? _timeOfDayController.TimeOfDay
                            : 0f;
                    });

                AudioEnvironmentController audioEnvController = new(
                    underwaterFilter,
                    enclosureProbe,
                    caveReverb,
                    biomeSampler,
                    _biomeAmbientPlayer,
                    scatterPlayer,
                    playerTransformForAudio);

                // Register audio environment tick adapter
                if (tickRegistryRef != null)
                {
                    tickRegistryRef.Register(
                        new AudioEnvironmentTickAdapter(audioEnvController));
                }

                // Wire all audio systems to GameLoop
                _gameLoop.SetAudioSystems(
                    footstepController, fallSoundDetector, _sfxSourcePool, audioEnvController);
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
                    _userPreferences,
                    _gameLoop.NotifyRenderDistanceChanged);

                if (audioMixerController != null)
                {
                    settingsScreenRef.SetAudioMixerController(audioMixerController);
                }

                settingsScreenRef.ApplyPersistedVolumes();
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
            return (uint)r << 24 | (uint)g << 16 | (uint)b << 8 | a;
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

            IReadOnlyList<StateRegistryEntry> entries =
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
    }
}
