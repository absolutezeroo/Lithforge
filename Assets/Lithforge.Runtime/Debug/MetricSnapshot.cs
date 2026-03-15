namespace Lithforge.Runtime.Debug
{
    /// <summary>
    /// Value-type snapshot of all diagnostics. Committed once per frame by MetricsRegistry.
    /// Read by F3DebugOverlay and BenchmarkRunner on the main thread — no lock needed.
    /// Flat fields with switch-based indexed access to avoid array allocation in the struct.
    /// </summary>
    public struct MetricSnapshot
    {
        // --- Frame timing ---
        public float FrameMs;
        public float FpsSmoothed;

        // --- FrameProfiler sections (14 values, indexed by FrameProfiler.* constants) ---
        public float SectionMs0;
        public float SectionMs1;
        public float SectionMs2;
        public float SectionMs3;
        public float SectionMs4;
        public float SectionMs5;
        public float SectionMs6;
        public float SectionMs7;
        public float SectionMs8;
        public float SectionMs9;
        public float SectionMs10;
        public float SectionMs11;
        public float SectionMs12;
        public float SectionMs13;

        // --- Pipeline counters (from PipelineStats) ---
        public int GenScheduled;
        public int GenCompleted;
        public int MeshScheduled;
        public int MeshCompleted;
        public int LodScheduled;
        public int LodCompleted;
        public int DecorateCount;
        public float DecorateMs;
        public long GpuUploadBytes;
        public int GpuUploadCount;
        public int GrowEvents;
        public int FreeListSize;
        public int InvalidateCount;
        public float MeshCompleteMaxMs;
        public int MeshCompleteStalls;
        public float GenCompleteMaxMs;
        public int GenCompleteStalls;

        // --- Cumulative pipeline counters ---
        public int TotalGenerated;
        public int TotalMeshed;
        public int TotalLod;
        public double TotalGpuUploadBytes;

        // --- GC ---
        public int GcGen0;
        public int GcGen1;
        public int GcGen2;

        // --- Chunk state histogram (8 entries, indexed by (int)ChunkState) ---
        public int ChunkState0;
        public int ChunkState1;
        public int ChunkState2;
        public int ChunkState3;
        public int ChunkState4;
        public int ChunkState5;
        public int ChunkState6;
        public int ChunkState7;
        public int NeedsRemeshCount;
        public int NeedsLightUpdateCount;

        // --- Pool ---
        public int PoolAvailable;
        public int PoolCheckedOut;
        public int PoolTotal;

        // --- VRAM (computed from MegaMeshBuffer capacities) ---
        public long VramTotalBytes;
        public int OpaqueUsedVerts;
        public int OpaqueCapacityVerts;
        public int CutoutUsedVerts;
        public int CutoutCapacityVerts;
        public int TranslucentUsedVerts;
        public int TranslucentCapacityVerts;

        // --- World / Player ---
        public float PlayerX;
        public float PlayerY;
        public float PlayerZ;
        public int ChunkX;
        public int ChunkY;
        public int ChunkZ;
        public int LoadedChunks;
        public int RendererCount;
        public bool OcclusionCullingActive;
        public bool IsFlying;
        public bool IsNoclip;
        public float FlySpeed;

        // --- Queues ---
        public int PendingGenCount;
        public int PendingMeshCount;
        public int PendingLodMeshCount;

        /// <summary>
        /// Retrieves a FrameProfiler section timing by index without array allocation.
        /// </summary>
        public float GetSectionMs(int index)
        {
            switch (index)
            {
                case 0: return SectionMs0;
                case 1: return SectionMs1;
                case 2: return SectionMs2;
                case 3: return SectionMs3;
                case 4: return SectionMs4;
                case 5: return SectionMs5;
                case 6: return SectionMs6;
                case 7: return SectionMs7;
                case 8: return SectionMs8;
                case 9: return SectionMs9;
                case 10: return SectionMs10;
                case 11: return SectionMs11;
                case 12: return SectionMs12;
                case 13: return SectionMs13;
                default: return 0f;
            }
        }

        /// <summary>
        /// Retrieves chunk state histogram count by index without array allocation.
        /// </summary>
        public int GetChunkState(int index)
        {
            switch (index)
            {
                case 0: return ChunkState0;
                case 1: return ChunkState1;
                case 2: return ChunkState2;
                case 3: return ChunkState3;
                case 4: return ChunkState4;
                case 5: return ChunkState5;
                case 6: return ChunkState6;
                case 7: return ChunkState7;
                default: return 0;
            }
        }
    }
}
