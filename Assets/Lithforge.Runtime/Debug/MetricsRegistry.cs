using Lithforge.Runtime.Input;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Session;
using Lithforge.Voxel.Chunk;
using UnityEngine;

namespace Lithforge.Runtime.Debug
{
    /// <summary>
    /// Aggregates all per-frame diagnostics from FrameProfiler, PipelineStats,
    /// ChunkManager, ChunkMeshStore, ChunkPool, and the player.
    /// CommitFrame() must be called once per Update cycle from GameLoopPoco.
    /// CurrentSnapshot is the last committed snapshot — reads are zero-alloc.
    /// Owner: LithforgeBootstrap. Lifetime: application session.
    /// </summary>
    public sealed class MetricsRegistry
    {
        private MetricSnapshot _current;

        // Rolling FPS smoother (exponential moving average)
        private float _fpsSmoothed;
        private float _fpsAlpha = 0.1f;

        // Frame-time ring buffer (300 samples) for graph and benchmark
        public const int HistorySize = 300;

        // External references set at Initialize time
        private ChunkMeshStore _chunkMeshStore;
        private ChunkPool _chunkPool;
        private PlayerController _playerController;
        private GameLoopPoco _gameLoopPoco;
        private IFrameProfiler _frameProfiler;
        private IPipelineStats _pipelineStats;

        // Scratch histogram — reused each frame (no alloc)
        private readonly int[] _histogramScratch = new int[8];

        public MetricSnapshot CurrentSnapshot
        {
            get { return _current; }
        }

        public MetricSnapshot PreviousSnapshot { get; private set; }

        public float[] FrameTimeHistory { get; } = new float[HistorySize];

        public int HistoryHead { get; private set; }

        public int HistoryFilled { get; private set; }

        public ChunkManager ChunkManager { get; private set; }

        public Camera MainCamera { get; private set; }

        public void Initialize(
            ChunkManager chunkManager,
            ChunkMeshStore chunkMeshStore,
            ChunkPool chunkPool,
            PlayerController playerController,
            Camera mainCamera,
            GameLoopPoco gameLoop,
            IFrameProfiler frameProfiler,
            IPipelineStats pipelineStats,
            float fpsAlpha)
        {
            ChunkManager = chunkManager;
            _chunkMeshStore = chunkMeshStore;
            _chunkPool = chunkPool;
            _playerController = playerController;
            MainCamera = mainCamera;
            _gameLoopPoco = gameLoop;
            _frameProfiler = frameProfiler;
            _pipelineStats = pipelineStats;
            _fpsAlpha = fpsAlpha;
        }

        /// <summary>
        /// Sets the game loop reference after late initialization.
        /// Called by SessionBridgeSubsystem after creating the GameLoopPoco.
        /// </summary>
        public void SetGameLoopPoco(GameLoopPoco gameLoopPoco)
        {
            _gameLoopPoco = gameLoopPoco;
        }

        /// <summary>
        /// Samples all sources and commits the snapshot.
        /// Call once per frame from GameLoopPoco.Update() AFTER all systems have run,
        /// FrameProfiler sections are closed, and PipelineStats counters are incremented.
        /// </summary>
        public void CommitFrame()
        {
            PreviousSnapshot = _current;

            float dt = Time.unscaledDeltaTime;
            float frameMs = dt * 1000f;
            float instantFps = dt > 0.0001f ? 1f / dt : 9999f;
            _fpsSmoothed = _fpsSmoothed + _fpsAlpha * (instantFps - _fpsSmoothed);

            // Frame-time ring buffer
            FrameTimeHistory[HistoryHead] = frameMs;
            HistoryHead = (HistoryHead + 1) % HistorySize;

            if (HistoryFilled < HistorySize)
            {
                HistoryFilled++;
            }

            // Frame timing
            _current.FrameMs = frameMs;
            _current.FpsSmoothed = _fpsSmoothed;

            // FrameProfiler sections
            _current.SectionMs0 = _frameProfiler.GetMs(0);
            _current.SectionMs1 = _frameProfiler.GetMs(1);
            _current.SectionMs2 = _frameProfiler.GetMs(2);
            _current.SectionMs3 = _frameProfiler.GetMs(3);
            _current.SectionMs4 = _frameProfiler.GetMs(4);
            _current.SectionMs5 = _frameProfiler.GetMs(5);
            _current.SectionMs6 = _frameProfiler.GetMs(6);
            _current.SectionMs7 = _frameProfiler.GetMs(7);
            _current.SectionMs8 = _frameProfiler.GetMs(8);
            _current.SectionMs9 = _frameProfiler.GetMs(9);
            _current.SectionMs10 = _frameProfiler.GetMs(10);
            _current.SectionMs11 = _frameProfiler.GetMs(11);
            _current.SectionMs12 = _frameProfiler.GetMs(12);
            _current.SectionMs13 = _frameProfiler.GetMs(13);
            _current.SectionMs14 = _frameProfiler.GetMs(14);

            // PipelineStats — per-frame
            _current.GenScheduled = _pipelineStats.GenScheduled;
            _current.GenCompleted = _pipelineStats.GenCompleted;
            _current.MeshScheduled = _pipelineStats.MeshScheduled;
            _current.MeshCompleted = _pipelineStats.MeshCompleted;
            _current.LodScheduled = _pipelineStats.LODScheduled;
            _current.LodCompleted = _pipelineStats.LODCompleted;
            _current.DecorateCount = _pipelineStats.DecorateCount;
            _current.DecorateMs = _pipelineStats.DecorateMs;
            _current.GpuUploadBytes = _pipelineStats.GpuUploadBytes;
            _current.GpuUploadCount = _pipelineStats.GpuUploadCount;
            _current.GrowEvents = _pipelineStats.GrowEvents;
            _current.InvalidateCount = _pipelineStats.InvalidateCount;
            _current.MeshCompleteMaxMs = _pipelineStats.MeshCompleteMaxMs;
            _current.MeshCompleteStalls = _pipelineStats.MeshCompleteStalls;
            _current.GenCompleteMaxMs = _pipelineStats.GenCompleteMaxMs;
            _current.GenCompleteStalls = _pipelineStats.GenCompleteStalls;

            // SchedMesh sub-timings
            _current.SchedMeshFillMs = _pipelineStats.SchedMeshFillMs;
            _current.SchedMeshFilterMs = _pipelineStats.SchedMeshFilterMs;
            _current.SchedMeshAllocMs = _pipelineStats.SchedMeshAllocMs;
            _current.SchedMeshScheduleMs = _pipelineStats.SchedMeshScheduleMs;
            _current.SchedMeshFlushMs = _pipelineStats.SchedMeshFlushMs;

            // PipelineStats — cumulative
            _current.TotalGenerated = _pipelineStats.TotalGenerated;
            _current.TotalMeshed = _pipelineStats.TotalMeshed;
            _current.TotalLod = _pipelineStats.TotalLOD;
            _current.TotalGpuUploadBytes = _pipelineStats.TotalGpuUploadBytes;

            // GC
            _current.GcGen0 = _pipelineStats.GcGen0;
            _current.GcGen1 = _pipelineStats.GcGen1;
            _current.GcGen2 = _pipelineStats.GcGen2;

            // Chunk histogram and pool
            if (ChunkManager != null && _chunkPool != null)
            {
                ChunkManager.FillStateHistogram(
                    _histogramScratch,
                    out int needsRemesh,
                    out int needsLightUpdate);

                _current.ChunkState0 = _histogramScratch[0];
                _current.ChunkState1 = _histogramScratch[1];
                _current.ChunkState2 = _histogramScratch[2];
                _current.ChunkState3 = _histogramScratch[3];
                _current.ChunkState4 = _histogramScratch[4];
                _current.ChunkState5 = _histogramScratch[5];
                _current.ChunkState6 = _histogramScratch[6];
                _current.ChunkState7 = _histogramScratch[7];
                _current.NeedsRemeshCount = needsRemesh;
                _current.NeedsLightUpdateCount = needsLightUpdate;

                _current.PoolAvailable = _chunkPool.AvailableCount;
                _current.PoolCheckedOut = _chunkPool.CheckedOutCount;
                _current.PoolTotal = _chunkPool.TotalAllocated;
                _current.LoadedChunks = ChunkManager.LoadedCount;
                _current.GeneratedSetSize = ChunkManager.GeneratedChunkCount;
            }

            // VRAM stats
            if (_chunkMeshStore != null)
            {
                _current.VramTotalBytes =
                    (long)_chunkMeshStore.OpaqueBuffer.VertexCapacity * 16
                    + (long)_chunkMeshStore.CutoutBuffer.VertexCapacity * 16
                    + (long)_chunkMeshStore.TranslucentBuffer.VertexCapacity * 16
                    + (long)_chunkMeshStore.OpaqueBuffer.IndexCapacity * 4
                    + (long)_chunkMeshStore.CutoutBuffer.IndexCapacity * 4
                    + (long)_chunkMeshStore.TranslucentBuffer.IndexCapacity * 4;

                _current.OpaqueUsedVerts = _chunkMeshStore.OpaqueBuffer.UsedVertices;
                _current.OpaqueCapacityVerts = _chunkMeshStore.OpaqueBuffer.VertexCapacity;
                _current.CutoutUsedVerts = _chunkMeshStore.CutoutBuffer.UsedVertices;
                _current.CutoutCapacityVerts = _chunkMeshStore.CutoutBuffer.VertexCapacity;
                _current.TranslucentUsedVerts = _chunkMeshStore.TranslucentBuffer.UsedVertices;
                _current.TranslucentCapacityVerts = _chunkMeshStore.TranslucentBuffer.VertexCapacity;

                _current.RendererCount = _chunkMeshStore.RendererCount;
                _current.OcclusionCullingActive = _chunkMeshStore.IsOcclusionCullingActive;
                _current.FreeListSize = _chunkMeshStore.OpaqueBuffer.FreeRegionCount;
            }

            // Player / world position
            if (MainCamera != null)
            {
                Vector3 pos = MainCamera.transform.position;
                _current.PlayerX = pos.x;
                _current.PlayerY = pos.y;
                _current.PlayerZ = pos.z;
                _current.ChunkX = Mathf.FloorToInt(pos.x / ChunkConstants.Size);
                _current.ChunkY = Mathf.FloorToInt(pos.y / ChunkConstants.Size);
                _current.ChunkZ = Mathf.FloorToInt(pos.z / ChunkConstants.Size);
            }

            if (_playerController != null)
            {
                _current.IsFlying = _playerController.IsFlying;
                _current.IsNoclip = _playerController.IsNoclip;
                _current.FlySpeed = _playerController.FlySpeed;
            }

            if (_gameLoopPoco != null)
            {
                _current.PendingGenCount = _gameLoopPoco.PendingGenerationCount;
                _current.PendingMeshCount = _gameLoopPoco.PendingMeshCount;
                _current.PendingLodMeshCount = _gameLoopPoco.PendingLODMeshCount;
                _current.TicksThisFrame = _gameLoopPoco.TicksThisFrame;
            }
        }
    }
}
