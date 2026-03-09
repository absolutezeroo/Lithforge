using System.Collections.Generic;
using Lithforge.Core.Logging;
using Lithforge.Core.Validation;
using Lithforge.Meshing;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Rendering;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Content;
using Lithforge.WorldGen.Noise;
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
        private StateRegistry _stateRegistry;
        private NativeStateRegistry _nativeStateRegistry;
        private ChunkPool _chunkPool;
        private ChunkManager _chunkManager;
        private GenerationPipeline _generationPipeline;
        private ChunkRenderManager _chunkRenderManager;
        private GameLoop _gameLoop;

        private void Awake()
        {
            _services = new ServiceContainer();

            InitializeContent();
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
            BlockDefinitionLoader loader = new BlockDefinitionLoader(logger, validator);

            string contentRoot = System.IO.Path.Combine(Application.streamingAssetsPath, "content", "lithforge");
            List<BlockDefinition> definitions = loader.LoadAll(contentRoot);

            _stateRegistry = new StateRegistry();

            for (int i = 0; i < definitions.Count; i++)
            {
                _stateRegistry.Register(definitions[i]);
            }

            _nativeStateRegistry = _stateRegistry.BakeNative(Allocator.Persistent);

            _services.Register(_stateRegistry);
            _services.Register(_nativeStateRegistry);

            UnityEngine.Debug.Log($"[Lithforge] Loaded {definitions.Count} blocks, {_stateRegistry.TotalStateCount} states.");
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

            StateId stoneId = FindStateId("lithforge:stone");
            StateId airId = StateId.Air;
            StateId waterId = FindStateId("lithforge:water");
            StateId grassId = FindStateId("lithforge:grass_block");
            StateId dirtId = FindStateId("lithforge:dirt");

            _generationPipeline = new GenerationPipeline(
                terrainNoise, stoneId, airId, waterId, grassId, dirtId, seaLevel);

            _services.Register(_generationPipeline);
        }

        private void InitializeRendering()
        {
            Material material = voxelMaterial;

            if (material == null)
            {
                Shader shader = Shader.Find("Lithforge/VoxelUnlit");

                if (shader != null)
                {
                    material = new Material(shader);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[Lithforge] VoxelUnlit shader not found, using default.");
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                }
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
                _nativeStateRegistry,
                _chunkRenderManager,
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
        }

        private StateId FindStateId(string idString)
        {
            string[] parts = idString.Split(':');
            string ns = parts[0];
            string name = parts[1];

            for (int i = 0; i < _stateRegistry.Entries.Count; i++)
            {
                StateRegistryEntry entry = _stateRegistry.Entries[i];

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

            if (_nativeStateRegistry.States.IsCreated)
            {
                _nativeStateRegistry.Dispose();
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
