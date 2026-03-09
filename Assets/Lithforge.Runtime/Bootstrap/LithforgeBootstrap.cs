using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Core.Logging;
using Lithforge.Core.Validation;
using Lithforge.Meshing.Atlas;
using Lithforge.Physics;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Rendering;
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

        [SerializeField] private int poolSize = 256;

        [SerializeField] private int renderDistance = 4;

        [SerializeField] private int seaLevel = 64;

        [SerializeField] private long seed = 42L;

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

            ContentPipeline pipeline = new ContentPipeline(logger, validator);
            string contentRoot = System.IO.Path.Combine(
                Application.streamingAssetsPath, "content", "lithforge");

            _contentResult = pipeline.Build(contentRoot);

            _services.Register(_contentResult.StateRegistry);
            _services.Register(_contentResult.NativeStateRegistry);

            UnityEngine.Debug.Log(
                $"[Lithforge] Content pipeline: {_contentResult.StateRegistry.TotalStateCount} states, " +
                $"{_contentResult.NativeAtlasLookup.TextureCount} textures, " +
                $"{_contentResult.BiomeDefinitions.Count} biomes, " +
                $"{_contentResult.OreDefinitions.Count} ores, " +
                $"{_contentResult.ItemDefinitions.Count} items, " +
                $"{_contentResult.LootTables.Count} loot tables, " +
                $"{_contentResult.TagRegistry.TagCount} tags.");
        }

        private void InitializeStorage()
        {
            string worldDir = System.IO.Path.Combine(
                Application.persistentDataPath, "worlds", "default");
            _worldStorage = new WorldStorage(worldDir);
            _worldStorage.SaveMetadata(seed, "");
            _services.Register(_worldStorage);

            UnityEngine.Debug.Log($"[Lithforge] World storage: {worldDir}");
        }

        private void InitializeChunkSystem()
        {
            _chunkPool = new ChunkPool(poolSize);
            _chunkManager = new ChunkManager(_chunkPool, renderDistance);

            _services.Register(_chunkPool);
            _services.Register(_chunkManager);
        }

        private void InitializeWorldGen()
        {
            NativeNoiseConfig terrainNoise = new NativeNoiseConfig
            {
                Frequency = 0.008f,
                Lacunarity = 2.0f,
                Persistence = 0.5f,
                HeightScale = 24.0f,
                Octaves = 5,
                SeedOffset = 0,
            };

            NativeNoiseConfig temperatureNoise = new NativeNoiseConfig
            {
                Frequency = 0.002f,
                Lacunarity = 2.0f,
                Persistence = 0.5f,
                HeightScale = 1.0f,
                Octaves = 3,
                SeedOffset = 999,
            };

            NativeNoiseConfig humidityNoise = new NativeNoiseConfig
            {
                Frequency = 0.002f,
                Lacunarity = 2.0f,
                Persistence = 0.5f,
                HeightScale = 1.0f,
                Octaves = 3,
                SeedOffset = 1999,
            };

            NativeNoiseConfig caveNoise = new NativeNoiseConfig
            {
                Frequency = 0.03f,
                Lacunarity = 2.0f,
                Persistence = 0.5f,
                HeightScale = 1.0f,
                Octaves = 2,
                SeedOffset = 0,
            };

            StateId stoneId = FindStateId("lithforge:stone");
            StateId airId = StateId.Air;
            StateId waterId = FindStateId("lithforge:water");

            // Build native biome data
            List<BiomeDefinition> biomeDefs = _contentResult.BiomeDefinitions;
            _nativeBiomeData = new NativeArray<NativeBiomeData>(
                biomeDefs.Count, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            for (int i = 0; i < biomeDefs.Count; i++)
            {
                BiomeDefinition def = biomeDefs[i];
                _nativeBiomeData[i] = new NativeBiomeData
                {
                    BiomeId = (byte)i,
                    TemperatureMin = def.TemperatureMin,
                    TemperatureMax = def.TemperatureMax,
                    TemperatureCenter = def.TemperatureCenter,
                    HumidityMin = def.HumidityMin,
                    HumidityMax = def.HumidityMax,
                    HumidityCenter = def.HumidityCenter,
                    TopBlock = FindStateId(def.TopBlock.ToString()),
                    FillerBlock = FindStateId(def.FillerBlock.ToString()),
                    StoneBlock = FindStateId(def.StoneBlock.ToString()),
                    UnderwaterBlock = FindStateId(def.UnderwaterBlock.ToString()),
                    FillerDepth = (byte)def.FillerDepth,
                    TreeDensity = def.TreeDensity,
                    HeightModifier = def.HeightModifier,
                };
            }

            // Build native ore configs
            List<OreDefinition> oreDefs = _contentResult.OreDefinitions;
            _nativeOreConfigs = new NativeArray<NativeOreConfig>(
                oreDefs.Count, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            for (int i = 0; i < oreDefs.Count; i++)
            {
                OreDefinition def = oreDefs[i];
                _nativeOreConfigs[i] = new NativeOreConfig
                {
                    OreStateId = FindStateId(def.OreBlock.ToString()),
                    ReplaceStateId = FindStateId(def.ReplaceBlock.ToString()),
                    MinY = def.MinY,
                    MaxY = def.MaxY,
                    VeinSize = def.VeinSize,
                    Frequency = def.Frequency,
                    OreType = (byte)(string.Equals(def.OreType, "scatter",
                        System.StringComparison.Ordinal) ? 0 : 1),
                };
            }

            _generationPipeline = new GenerationPipeline(
                terrainNoise,
                temperatureNoise,
                humidityNoise,
                caveNoise,
                _nativeBiomeData,
                _nativeOreConfigs,
                _contentResult.NativeStateRegistry.States,
                stoneId, airId, waterId,
                seaLevel);

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
                seed);

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
                playerObject.transform.position = new Vector3(0, seaLevel + 32, 0);

                // Reparent camera under player
                mainCamera.transform.SetParent(playerObject.transform);
                mainCamera.transform.localPosition = new Vector3(
                    0f, Lithforge.Physics.PhysicsConstants.PlayerEyeHeight, 0f);
                mainCamera.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
                mainCamera.farClipPlane = 500f;

                // Add PlayerController to player object
                PlayerController playerController = playerObject.AddComponent<PlayerController>();
                playerController.Initialize(
                    _chunkManager, _contentResult.NativeStateRegistry, _gameLoop);
                _services.Register(playerController);

                // Register player transform for safe spawn placement
                _gameLoop.SetPlayerTransform(playerObject.transform);

                // Add CameraController to camera
                CameraController cameraController = mainCamera.gameObject.AddComponent<CameraController>();
                _services.Register(cameraController);

                // Add BlockHighlight (standalone object)
                GameObject highlightObject = new GameObject("BlockHighlight");
                BlockHighlight blockHighlight = highlightObject.AddComponent<BlockHighlight>();

                // Create player inventory and loot resolver
                Inventory playerInventory = new Inventory();
                LootResolver lootResolver = new LootResolver(_contentResult.LootTables);

                // Give player some starting cobblestone
                ResourceId cobblestoneId = new Lithforge.Core.Data.ResourceId("lithforge", "cobblestone");
                playerInventory.AddItem(cobblestoneId, 64, 64);

                // Add BlockInteraction to camera (raycasts from camera position/direction)
                BlockInteraction blockInteraction = mainCamera.gameObject.AddComponent<BlockInteraction>();
                blockInteraction.Initialize(
                    _chunkManager,
                    _contentResult.NativeStateRegistry,
                    _contentResult.StateRegistry,
                    blockHighlight,
                    playerInventory,
                    _contentResult.ItemRegistry,
                    lootResolver);
                _services.Register(blockInteraction);

                // Load shared PanelSettings asset for UI Toolkit
                UnityEngine.UIElements.PanelSettings panelSettings =
                    Resources.Load<UnityEngine.UIElements.PanelSettings>("DefaultPanelSettings");

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
            }

            // Add debug HUD
            DebugOverlayHUD hud = gameObject.AddComponent<DebugOverlayHUD>();
            hud.Initialize(_gameLoop, _chunkManager);

            // Initialize day/night cycle
            Material material = _chunkRenderManager.OpaqueMaterial;

            if (material != null)
            {
                _timeOfDayController = gameObject.AddComponent<TimeOfDayController>();
                _timeOfDayController.Initialize(material, _chunkRenderManager.TranslucentMaterial);
            }
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

                if (entry.Definition.Id.Namespace == ns && entry.Definition.Id.Name == name)
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
                _worldStorage.SaveMetadata(seed, "");
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

    internal sealed class UnityLogger : Lithforge.Core.Logging.ILogger
    {
        public void Log(Lithforge.Core.Logging.LogLevel level, string message)
        {
            switch (level)
            {
                case Lithforge.Core.Logging.LogLevel.Debug:
                    UnityEngine.Debug.Log($"[DEBUG] {message}");
                    break;
                case Lithforge.Core.Logging.LogLevel.Info:
                    UnityEngine.Debug.Log($"[INFO] {message}");
                    break;
                case Lithforge.Core.Logging.LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(message);
                    break;
                case Lithforge.Core.Logging.LogLevel.Error:
                    UnityEngine.Debug.LogError(message);
                    break;
                default:
                    UnityEngine.Debug.Log(message);
                    break;
            }
        }

        public void LogDebug(string message)
        {
            UnityEngine.Debug.Log($"[DEBUG] {message}");
        }

        public void LogInfo(string message)
        {
            UnityEngine.Debug.Log($"[INFO] {message}");
        }

        public void LogWarning(string message)
        {
            UnityEngine.Debug.LogWarning(message);
        }

        public void LogError(string message)
        {
            UnityEngine.Debug.LogError(message);
        }
    }
}
