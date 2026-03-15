using Lithforge.Runtime.Input;
using Lithforge.Runtime.Rendering;
using Lithforge.Voxel.Chunk;
using UnityEngine;

namespace Lithforge.Runtime.Debug
{
    /// <summary>
    /// Aggregates all per-frame diagnostics from FrameProfiler, PipelineStats,
    /// ChunkManager, ChunkMeshStore, ChunkPool, and the player.
    /// CommitFrame() must be called once per Update cycle from GameLoop.
    /// CurrentSnapshot is the last committed snapshot — reads are zero-alloc.
    /// Owner: LithforgeBootstrap. Lifetime: application session.
    /// </summary>
    public sealed class MetricsRegistry
    {
        private MetricSnapshot _current;
        private MetricSnapshot _previous;

        // Rolling FPS smoother (exponential moving average)
        private float _fpsSmoothed;
        private float _fpsAlpha = 0.1f;

        // Frame-time ring buffer (300 samples) for graph and benchmark
        public const int HistorySize = 300;
        private readonly float[] _frameTimeHistory = new float[HistorySize];
        private int _historyHead;
        private int _historyFilled;

        // External references set at Initialize time
        private ChunkManager _chunkManager;
        private ChunkMeshStore _chunkMeshStore;
        private ChunkPool _chunkPool;
        private PlayerController _playerController;
        private Camera _mainCamera;
        private GameLoop _gameLoop;

        // Scratch histogram — reused each frame (no alloc)
        private readonly int[] _histogramScratch = new int[8];

        public MetricSnapshot CurrentSnapshot
        {
            get { return _current; }
        }

        public MetricSnapshot PreviousSnapshot
        {
            get { return _previous; }
        }

        public float[] FrameTimeHistory
        {
            get { return _frameTimeHistory; }
        }

        public int HistoryHead
        {
            get { return _historyHead; }
        }

        public int HistoryFilled
        {
            get { return _historyFilled; }
        }

        public ChunkManager ChunkManager
        {
            get { return _chunkManager; }
        }

        public Camera MainCamera
        {
            get { return _mainCamera; }
        }

        public void Initialize(
            ChunkManager chunkManager,
            ChunkMeshStore chunkMeshStore,
            ChunkPool chunkPool,
            PlayerController playerController,
            Camera mainCamera,
            GameLoop gameLoop,
            float fpsAlpha)
        {
            _chunkManager = chunkManager;
            _chunkMeshStore = chunkMeshStore;
            _chunkPool = chunkPool;
            _playerController = playerController;
            _mainCamera = mainCamera;
            _gameLoop = gameLoop;
            _fpsAlpha = fpsAlpha;
        }

        /// <summary>
        /// Samples all sources and commits the snapshot.
        /// Call once per frame from GameLoop.Update() after FrameProfiler.BeginFrame()
        /// and PipelineStats.BeginFrame() have run.
        /// </summary>
        public void CommitFrame()
        {
            _previous = _current;

            float dt = Time.unscaledDeltaTime;
            float frameMs = dt * 1000f;
            float instantFps = dt > 0.0001f ? 1f / dt : 9999f;
            _fpsSmoothed = _fpsSmoothed + _fpsAlpha * (instantFps - _fpsSmoothed);

            // Frame-time ring buffer
            _frameTimeHistory[_historyHead] = frameMs;
            _historyHead = (_historyHead + 1) % HistorySize;

            if (_historyFilled < HistorySize)
            {
                _historyFilled++;
            }

            // Frame timing
            _current.FrameMs = frameMs;
            _current.FpsSmoothed = _fpsSmoothed;

            // FrameProfiler sections
            _current.SectionMs0 = FrameProfiler.GetMs(0);
            _current.SectionMs1 = FrameProfiler.GetMs(1);
            _current.SectionMs2 = FrameProfiler.GetMs(2);
            _current.SectionMs3 = FrameProfiler.GetMs(3);
            _current.SectionMs4 = FrameProfiler.GetMs(4);
            _current.SectionMs5 = FrameProfiler.GetMs(5);
            _current.SectionMs6 = FrameProfiler.GetMs(6);
            _current.SectionMs7 = FrameProfiler.GetMs(7);
            _current.SectionMs8 = FrameProfiler.GetMs(8);
            _current.SectionMs9 = FrameProfiler.GetMs(9);
            _current.SectionMs10 = FrameProfiler.GetMs(10);
            _current.SectionMs11 = FrameProfiler.GetMs(11);
            _current.SectionMs12 = FrameProfiler.GetMs(12);
            _current.SectionMs13 = FrameProfiler.GetMs(13);

            // PipelineStats — per-frame
            _current.GenScheduled = PipelineStats.GenScheduled;
            _current.GenCompleted = PipelineStats.GenCompleted;
            _current.MeshScheduled = PipelineStats.MeshScheduled;
            _current.MeshCompleted = PipelineStats.MeshCompleted;
            _current.LodScheduled = PipelineStats.LODScheduled;
            _current.LodCompleted = PipelineStats.LODCompleted;
            _current.DecorateCount = PipelineStats.DecorateCount;
            _current.DecorateMs = PipelineStats.DecorateMs;
            _current.GpuUploadBytes = PipelineStats.GpuUploadBytes;
            _current.GpuUploadCount = PipelineStats.GpuUploadCount;
            _current.GrowEvents = PipelineStats.GrowEvents;
            _current.InvalidateCount = PipelineStats.InvalidateCount;
            _current.MeshCompleteMaxMs = PipelineStats.MeshCompleteMaxMs;
            _current.MeshCompleteStalls = PipelineStats.MeshCompleteStalls;
            _current.GenCompleteMaxMs = PipelineStats.GenCompleteMaxMs;
            _current.GenCompleteStalls = PipelineStats.GenCompleteStalls;

            // PipelineStats — cumulative
            _current.TotalGenerated = PipelineStats.TotalGenerated;
            _current.TotalMeshed = PipelineStats.TotalMeshed;
            _current.TotalLod = PipelineStats.TotalLOD;
            _current.TotalGpuUploadBytes = PipelineStats.TotalGpuUploadBytes;

            // GC
            _current.GcGen0 = PipelineStats.GcGen0;
            _current.GcGen1 = PipelineStats.GcGen1;
            _current.GcGen2 = PipelineStats.GcGen2;

            // Chunk histogram and pool
            if (_chunkManager != null && _chunkPool != null)
            {
                _chunkManager.FillStateHistogram(
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
                _current.LoadedChunks = _chunkManager.LoadedCount;
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
            if (_mainCamera != null)
            {
                Vector3 pos = _mainCamera.transform.position;
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

            if (_gameLoop != null)
            {
                _current.PendingGenCount = _gameLoop.PendingGenerationCount;
                _current.PendingMeshCount = _gameLoop.PendingMeshCount;
                _current.PendingLodMeshCount = _gameLoop.PendingLODMeshCount;
            }
        }
    }
}
