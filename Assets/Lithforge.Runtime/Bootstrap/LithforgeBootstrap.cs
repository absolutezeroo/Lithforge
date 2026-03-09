using Lithforge.Core.Logging;
using Lithforge.Core.Validation;
using Lithforge.Meshing.Atlas;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Rendering;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
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
        private ContentPipelineResult _contentResult;
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
                $"{_contentResult.NativeAtlasLookup.TextureCount} textures.");
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

            StateId stoneId = FindStateId("lithforge:stone");
            StateId airId = StateId.Air;
            StateId waterId = FindStateId("lithforge:water");
            StateId grassId = FindStateId("lithforge:grass_block");
            StateId dirtId = FindStateId("lithforge:dirt");

            _generationPipeline = new GenerationPipeline(
                terrainNoise,
                stoneId, airId, waterId, grassId, dirtId,
                seaLevel);

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
