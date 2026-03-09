using System.Collections.Generic;
using Lithforge.Core.Logging;
using Lithforge.Core.Validation;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Rendering;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
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
            UnityLogger logger = new();
            ContentValidator validator = new();

            ContentPipeline pipeline = new(logger, validator);
            string contentRoot = System.IO.Path.Combine(
                Application.streamingAssetsPath, "content", "lithforge");

            _contentResult = pipeline.Build(contentRoot);

            _services.Register(_contentResult.StateRegistry);
            _services.Register(_contentResult.NativeStateRegistry);

            UnityEngine.Debug.Log(
                $"[Lithforge] Content pipeline: {_contentResult.StateRegistry.TotalStateCount} states, " +
                $"{_contentResult.NativeAtlasLookup.TextureCount} textures, " +
                $"{_contentResult.BiomeDefinitions.Count} biomes, " +
                $"{_contentResult.OreDefinitions.Count} ores.");
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
            NativeNoiseConfig terrainNoise = new()
            {
                Frequency = 0.008f,
                Lacunarity = 2.0f,
                Persistence = 0.5f,
                HeightScale = 24.0f,
                Octaves = 5,
                SeedOffset = 0,
            };

            NativeNoiseConfig temperatureNoise = new()
            {
                Frequency = 0.002f,
                Lacunarity = 2.0f,
                Persistence = 0.5f,
                HeightScale = 1.0f,
                Octaves = 3,
                SeedOffset = 999,
            };

            NativeNoiseConfig humidityNoise = new()
            {
                Frequency = 0.002f,
                Lacunarity = 2.0f,
                Persistence = 0.5f,
                HeightScale = 1.0f,
                Octaves = 3,
                SeedOffset = 1999,
            };

            NativeNoiseConfig caveNoise = new()
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
            Material material = voxelMaterial;

            if (material == null)
            {
                Shader shader = Shader.Find("Lithforge/VoxelOpaque");

                if (shader == null)
                {
                    // Fallback to old shader
                    shader = Shader.Find("Lithforge/VoxelUnlit");
                }

                if (shader != null)
                {
                    material = new Material(shader);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[Lithforge] VoxelOpaque shader not found, using default.");

                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                }
            }

            // Assign atlas texture to material
            if (_contentResult.AtlasResult != null && _contentResult.AtlasResult.TextureArray != null)
            {
                material.SetTexture("_AtlasArray", _contentResult.AtlasResult.TextureArray);
            }

            _chunkRenderManager = new ChunkRenderManager(material);
            _services.Register(_chunkRenderManager);
        }

        private void InitializeGameLoop()
        {
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

            // Add debug HUD
            DebugOverlayHUD hud = gameObject.AddComponent<DebugOverlayHUD>();
            hud.Initialize(_gameLoop, _chunkManager);

            // Setup camera with FPS controller if main camera exists
            Camera mainCamera = Camera.main;

            if (mainCamera != null && mainCamera.GetComponent<FPSCameraController>() == null)
            {
                mainCamera.gameObject.AddComponent<FPSCameraController>();
            }

            // Position camera above terrain
            if (mainCamera != null)
            {
                mainCamera.transform.position = new Vector3(0, seaLevel + 32, 0);
                mainCamera.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
                mainCamera.farClipPlane = 500f;
            }

            // Initialize day/night cycle
            Material material = _chunkRenderManager.Material;

            if (material != null)
            {
                _timeOfDayController = gameObject.AddComponent<TimeOfDayController>();
                _timeOfDayController.Initialize(material);
            }
        }

        private StateId FindStateId(string idString)
        {
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
